using System.Net;
using System.Net.Http.Json;
using Folio.Api.Contracts;
using Folio.Infrastructure.Persistence;

namespace Folio.Tests;

/// <summary>Activity records + notification fan-out (mentions, authored pages), unread counts.</summary>
public class NotificationTests : IDisposable
{
    private static readonly Guid WorkspaceId = DbSeeder.WorkspaceId;
    private static readonly Guid PageId = DbSeeder.GettingStartedId; // authored by the Acme owner (Ada)
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

    [Fact]
    public async Task Comment_notifies_the_page_author()
    {
        var owner = _factory.CreateAuthenticatedClient(DbSeeder.OwnerEmail);
        var editor = _factory.CreateAuthenticatedClient(DbSeeder.EditorEmail);

        // Grace (editor) comments on a page Ada authored → Ada gets a notification.
        await editor.PostAsJsonAsync($"/api/pages/{PageId}/comments", new { body = "looks good" });

        var owned = await owner.GetFromJsonAsync<List<NotificationResponse>>("/api/notifications", TestJson.Options);
        Assert.Contains(owned!, n => n.Type == "CommentCreated" && n.PageId == PageId);

        var count = await owner.GetFromJsonAsync<UnreadCountResponse>("/api/notifications/unread-count", TestJson.Options);
        Assert.True(count!.Count >= 1);
    }

    [Fact]
    public async Task Comment_does_not_notify_its_own_author()
    {
        var owner = _factory.CreateAuthenticatedClient(DbSeeder.OwnerEmail);

        // Ada comments on her own page — she should not be notified.
        await owner.PostAsJsonAsync($"/api/pages/{PageId}/comments", new { body = "note to self" });

        var count = await owner.GetFromJsonAsync<UnreadCountResponse>("/api/notifications/unread-count", TestJson.Options);
        Assert.Equal(0, count!.Count);
    }

    [Fact]
    public async Task Mention_notifies_the_mentioned_member()
    {
        var owner = _factory.CreateAuthenticatedClient(DbSeeder.OwnerEmail);
        var viewer = _factory.CreateAuthenticatedClient(DbSeeder.ViewerEmail);
        var linusId = await MemberIdAsync(owner, DbSeeder.ViewerEmail);

        await owner.PostAsJsonAsync(
            $"/api/pages/{PageId}/comments",
            new { body = $"cc @[Linus Torvalds]({linusId})" });

        var linusNotes = await viewer.GetFromJsonAsync<List<NotificationResponse>>("/api/notifications", TestJson.Options);
        Assert.Contains(linusNotes!, n => n.Type == "CommentCreated");
    }

    [Fact]
    public async Task Mark_read_and_read_all_clear_unread_count()
    {
        var owner = _factory.CreateAuthenticatedClient(DbSeeder.OwnerEmail);
        var editor = _factory.CreateAuthenticatedClient(DbSeeder.EditorEmail);

        await editor.PostAsJsonAsync($"/api/pages/{PageId}/comments", new { body = "one" });
        await editor.PostAsJsonAsync($"/api/pages/{PageId}/comments", new { body = "two" });

        var notes = await owner.GetFromJsonAsync<List<NotificationResponse>>("/api/notifications", TestJson.Options);
        Assert.True(notes!.Count >= 2);

        var readOne = await owner.PostAsync($"/api/notifications/{notes[0].Id}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, readOne.StatusCode);

        await owner.PostAsync("/api/notifications/read-all", null);
        var count = await owner.GetFromJsonAsync<UnreadCountResponse>("/api/notifications/unread-count", TestJson.Options);
        Assert.Equal(0, count!.Count);
    }

    [Fact]
    public async Task Activity_feed_records_page_and_comment_mutations()
    {
        var owner = _factory.CreateAuthenticatedClient(DbSeeder.OwnerEmail);

        await owner.PostAsJsonAsync($"/api/workspaces/{WorkspaceId}/pages", new { title = "Activity Test" });
        await owner.PostAsJsonAsync($"/api/pages/{PageId}/comments", new { body = "hi" });

        var feed = await owner.GetFromJsonAsync<List<ActivityResponse>>(
            $"/api/workspaces/{WorkspaceId}/activity", TestJson.Options);

        Assert.Contains(feed!, a => a.Type == "PageCreated");
        Assert.Contains(feed!, a => a.Type == "CommentCreated");
    }

    [Fact]
    public async Task Notifications_are_scoped_to_the_recipient()
    {
        var owner = _factory.CreateAuthenticatedClient(DbSeeder.OwnerEmail);
        var editor = _factory.CreateAuthenticatedClient(DbSeeder.EditorEmail);

        // Editor comments on Ada's page → notifies Ada, not the editor.
        await editor.PostAsJsonAsync($"/api/pages/{PageId}/comments", new { body = "ping" });

        var editorNotes = await editor.GetFromJsonAsync<List<NotificationResponse>>("/api/notifications", TestJson.Options);
        Assert.Empty(editorNotes!);
    }
}
