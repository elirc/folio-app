using System.Net;
using System.Net.Http.Json;
using Folio.Api.Contracts;
using Folio.Infrastructure.Persistence;

namespace Folio.Tests;

/// <summary>Page/block comment threads: create, reply, resolve, delete, and @mention parsing.</summary>
public class CommentTests : IDisposable
{
    private static readonly Guid PageId = DbSeeder.GettingStartedId;
    private readonly FolioApiFactory _factory = new();
    private readonly HttpClient _client;

    public CommentTests() => _client = _factory.CreateAuthenticatedClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private Task<List<CommentResponse>> ListAsync() =>
        _client.GetFromJsonAsync<List<CommentResponse>>($"/api/pages/{PageId}/comments", TestJson.Options)!;

    private async Task<CommentResponse> CreateAsync(object body)
    {
        var response = await _client.PostAsJsonAsync($"/api/pages/{PageId}/comments", body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CommentResponse>(TestJson.Options))!;
    }

    private async Task<Guid> GraceIdAsync()
    {
        var members = await _client.GetFromJsonAsync<List<MemberResponse>>(
            $"/api/workspaces/{DbSeeder.WorkspaceId}/members", TestJson.Options);
        return members!.First(m => m.Email == DbSeeder.EditorEmail).Id;
    }

    [Fact]
    public async Task Create_page_level_comment_shows_author()
    {
        var comment = await CreateAsync(new { body = "First!" });

        Assert.Null(comment.BlockId);
        Assert.Equal("Ada Lovelace", comment.AuthorName);
        Assert.False(comment.IsResolved);

        var list = await ListAsync();
        Assert.Single(list, c => c.Id == comment.Id);
    }

    [Fact]
    public async Task Create_block_level_comment_anchors_to_block()
    {
        var blocks = await _client.GetFromJsonAsync<List<BlockResponse>>(
            $"/api/pages/{PageId}/blocks", TestJson.Options);
        var blockId = blocks![0].Id;

        var comment = await CreateAsync(new { body = "About this heading", blockId });

        Assert.Equal(blockId, comment.BlockId);
    }

    [Fact]
    public async Task Reply_threads_under_a_parent_comment()
    {
        var root = await CreateAsync(new { body = "Thread root" });
        var reply = await CreateAsync(new { body = "A reply", parentCommentId = root.Id });

        Assert.Equal(root.Id, reply.ParentCommentId);
    }

    [Fact]
    public async Task Mention_token_is_parsed_and_stored_as_reference()
    {
        var graceId = await GraceIdAsync();

        var comment = await CreateAsync(new { body = $"cc @[Grace Hopper]({graceId}) please look" });

        var mention = Assert.Single(comment.Mentions);
        Assert.Equal(graceId, mention.MemberId);
        Assert.Equal("Grace Hopper", mention.Name);
    }

    [Fact]
    public async Task Resolve_and_unresolve_toggles_state()
    {
        var comment = await CreateAsync(new { body = "Resolve me" });

        var resolved = await _client.PostAsync($"/api/comments/{comment.Id}/resolve", null);
        var resolvedBody = await resolved.Content.ReadFromJsonAsync<CommentResponse>(TestJson.Options);
        Assert.True(resolvedBody!.IsResolved);
        Assert.NotNull(resolvedBody.ResolvedAt);

        var reopened = await _client.PostAsync($"/api/comments/{comment.Id}/unresolve", null);
        var reopenedBody = await reopened.Content.ReadFromJsonAsync<CommentResponse>(TestJson.Options);
        Assert.False(reopenedBody!.IsResolved);
        Assert.Null(reopenedBody.ResolvedAt);
    }

    [Fact]
    public async Task Delete_removes_comment_and_its_replies()
    {
        var root = await CreateAsync(new { body = "Doomed thread" });
        await CreateAsync(new { body = "reply", parentCommentId = root.Id });

        var delete = await _client.DeleteAsync($"/api/comments/{root.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var list = await ListAsync();
        Assert.DoesNotContain(list, c => c.Id == root.Id);
        Assert.DoesNotContain(list, c => c.ParentCommentId == root.Id);
    }

    [Fact]
    public async Task Viewer_can_comment_but_only_delete_their_own()
    {
        // Owner comments; a Viewer cannot delete it.
        var ownerComment = await CreateAsync(new { body = "owner note" });

        var viewer = _factory.CreateAuthenticatedClient(DbSeeder.ViewerEmail);
        var viewerComment = await viewer.PostAsJsonAsync(
            $"/api/pages/{PageId}/comments",
            new { body = "viewer can comment" });
        Assert.Equal(HttpStatusCode.Created, viewerComment.StatusCode);

        var blocked = await viewer.DeleteAsync($"/api/comments/{ownerComment.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
    }
}
