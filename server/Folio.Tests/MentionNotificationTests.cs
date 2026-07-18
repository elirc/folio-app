using System.Net;
using System.Net.Http.Json;
using Folio.Api.Contracts;
using Folio.Infrastructure.Persistence;

namespace Folio.Tests;

/// <summary>
/// Mention-parsing edge cases and notification fan-out invariants: malformed
/// tokens produce no mentions, a recipient who qualifies several ways is notified
/// exactly once, self-mentions never notify, and mark-read is idempotent.
/// </summary>
public class MentionNotificationTests : IDisposable
{
    private static readonly Guid WorkspaceId = DbSeeder.WorkspaceId;
    private static readonly Guid PageId = DbSeeder.GettingStartedId; // authored by Ada (owner)
    private readonly FolioApiFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<Guid> MemberIdAsync(HttpClient client, string email)
    {
        var members = await client.GetFromJsonAsync<List<MemberResponse>>(
            $"/api/workspaces/{WorkspaceId}/members", TestJson.Options);
        return members!.First(m => m.Email == email).Id;
    }

    private static async Task<int> UnreadAsync(HttpClient client)
    {
        var count = await client.GetFromJsonAsync<UnreadCountResponse>(
            "/api/notifications/unread-count", TestJson.Options);
        return count!.Count;
    }

    private static async Task<CommentResponse> CommentAsync(HttpClient client, Guid pageId, string body)
    {
        var response = await client.PostAsJsonAsync($"/api/pages/{pageId}/comments", new { body });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CommentResponse>(TestJson.Options))!;
    }

    // ---- mention parsing ----

    [Theory]
    [InlineData("just plain text, no mention")]
    [InlineData("missing parens @[Grace Hopper]")]
    [InlineData("bad id @[Grace Hopper](not-a-guid)")]
    [InlineData("half token @[Grace Hopper](11111111-1111)")]
    public async Task Malformed_mention_tokens_produce_no_mentions(string body)
    {
        var client = _factory.CreateAuthenticatedClient(DbSeeder.OwnerEmail);
        var comment = await CommentAsync(client, PageId, body);
        Assert.Empty(comment.Mentions);
    }

    [Fact]
    public async Task Mention_of_a_non_member_guid_is_dropped()
    {
        var client = _factory.CreateAuthenticatedClient(DbSeeder.OwnerEmail);
        // Well-formed token, but the id belongs to no workspace member.
        var comment = await CommentAsync(client, PageId, $"cc @[Ghost]({Guid.NewGuid()})");
        Assert.Empty(comment.Mentions);
    }

    [Fact]
    public async Task Well_formed_mention_of_a_member_is_stored_once_even_if_repeated()
    {
        var owner = _factory.CreateAuthenticatedClient(DbSeeder.OwnerEmail);
        var graceId = await MemberIdAsync(owner, DbSeeder.EditorEmail);

        // The same member mentioned twice collapses to a single reference.
        var comment = await CommentAsync(owner, PageId,
            $"@[Grace Hopper]({graceId}) and again @[Grace Hopper]({graceId})");

        var mention = Assert.Single(comment.Mentions);
        Assert.Equal(graceId, mention.MemberId);
    }

    // ---- fan-out exactly-once ----

    [Fact]
    public async Task Author_who_is_also_mentioned_is_notified_exactly_once_per_comment()
    {
        var owner = _factory.CreateAuthenticatedClient(DbSeeder.OwnerEmail);
        var editor = _factory.CreateAuthenticatedClient(DbSeeder.EditorEmail);
        var adaId = await MemberIdAsync(owner, DbSeeder.OwnerEmail);

        // First comment by Grace notifies Ada (page author): Ada now has 1.
        await CommentAsync(editor, PageId, "first");
        Assert.Equal(1, await UnreadAsync(owner));

        // Second comment mentions Ada AND Ada is the author AND ... — all dedup to
        // a single notification for that comment. Ada goes 1 → 2, not 3.
        await CommentAsync(editor, PageId, $"cc @[Ada Lovelace]({adaId}) about the author page");
        Assert.Equal(2, await UnreadAsync(owner));
    }

    [Fact]
    public async Task Prior_commenters_are_notified_but_the_actor_is_not()
    {
        var owner = _factory.CreateAuthenticatedClient(DbSeeder.OwnerEmail);
        var editor = _factory.CreateAuthenticatedClient(DbSeeder.EditorEmail);
        var viewer = _factory.CreateAuthenticatedClient(DbSeeder.ViewerEmail);

        // Grace comments first (becomes a prior commenter, notifies author Ada).
        await CommentAsync(editor, PageId, "grace here");
        // Linus comments next → notifies Ada (author) + Grace (prior commenter),
        // but not Linus himself.
        await CommentAsync(viewer, PageId, "linus here");

        Assert.Equal(2, await UnreadAsync(owner));  // author, two comments
        Assert.Equal(1, await UnreadAsync(editor));  // prior commenter, one fan-out
        Assert.Equal(0, await UnreadAsync(viewer));  // actor is never self-notified
    }

    [Fact]
    public async Task Mentioning_yourself_does_not_notify_you()
    {
        var owner = _factory.CreateAuthenticatedClient(DbSeeder.OwnerEmail);
        var adaId = await MemberIdAsync(owner, DbSeeder.OwnerEmail);

        await CommentAsync(owner, PageId, $"note to self @[Ada Lovelace]({adaId})");

        Assert.Equal(0, await UnreadAsync(owner));
    }

    // ---- mark-read idempotency ----

    [Fact]
    public async Task Marking_a_notification_read_twice_is_idempotent()
    {
        var owner = _factory.CreateAuthenticatedClient(DbSeeder.OwnerEmail);
        var editor = _factory.CreateAuthenticatedClient(DbSeeder.EditorEmail);

        await CommentAsync(editor, PageId, "one");
        await CommentAsync(editor, PageId, "two");
        Assert.Equal(2, await UnreadAsync(owner));

        var notes = await owner.GetFromJsonAsync<List<NotificationResponse>>("/api/notifications", TestJson.Options);
        var first = notes![0].Id;

        // Reading the same notification twice decrements the count only once.
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"/api/notifications/{first}/read", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"/api/notifications/{first}/read", null)).StatusCode);
        Assert.Equal(1, await UnreadAsync(owner));

        // read-all then read-all again leaves the count pinned at zero.
        await owner.PostAsync("/api/notifications/read-all", null);
        await owner.PostAsync("/api/notifications/read-all", null);
        Assert.Equal(0, await UnreadAsync(owner));
    }

    [Fact]
    public async Task A_member_cannot_mark_another_members_notification_read()
    {
        var owner = _factory.CreateAuthenticatedClient(DbSeeder.OwnerEmail);
        var editor = _factory.CreateAuthenticatedClient(DbSeeder.EditorEmail);

        await CommentAsync(editor, PageId, "for ada");
        var adaNotes = await owner.GetFromJsonAsync<List<NotificationResponse>>("/api/notifications", TestJson.Options);
        var adaNote = adaNotes!.First().Id;

        // Grace tries to mark Ada's notification read → 404 (not hers).
        var response = await editor.PostAsync($"/api/notifications/{adaNote}/read", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // Ada's unread count is untouched.
        Assert.Equal(1, await UnreadAsync(owner));
    }
}
