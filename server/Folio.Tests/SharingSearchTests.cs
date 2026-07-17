using System.Net;
using System.Net.Http.Json;
using Folio.Api.Contracts;
using Folio.Domain.Enums;
using Folio.Infrastructure.Persistence;

namespace Folio.Tests;

public class SharingSearchTests : IDisposable
{
    private static readonly Guid WorkspaceId = DbSeeder.WorkspaceId;
    private readonly FolioApiFactory _factory = new();
    private readonly HttpClient _client;

    public SharingSearchTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private Task<List<PageTreeNode>> TreeAsync() =>
        _client.GetFromJsonAsync<List<PageTreeNode>>($"/api/workspaces/{WorkspaceId}/pages/tree")!;

    // ---- trash / restore ----

    [Fact]
    public async Task Delete_moves_subtree_to_trash_and_restore_brings_it_back()
    {
        await _client.DeleteAsync($"/api/pages/{DbSeeder.GettingStartedId}");

        var tree = await TreeAsync();
        Assert.DoesNotContain(tree, n => n.Id == DbSeeder.GettingStartedId);

        var trash = await _client.GetFromJsonAsync<List<TrashItemResponse>>(
            $"/api/workspaces/{WorkspaceId}/trash");
        Assert.Contains(trash!, t => t.Id == DbSeeder.GettingStartedId);
        // Children are trashed too but only the subtree root is listed.
        Assert.DoesNotContain(trash!, t => t.Id == DbSeeder.InstallationId);

        var restore = await _client.PostAsync($"/api/pages/{DbSeeder.GettingStartedId}/restore", null);
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);

        var restoredTree = await TreeAsync();
        var restored = restoredTree.First(n => n.Id == DbSeeder.GettingStartedId);
        Assert.Equal(2, restored.Children.Count); // Installation + Configuration returned
    }

    // ---- sharing / public link ----

    [Fact]
    public async Task Share_public_then_revoke_toggles_public_access()
    {
        var shareResponse = await _client.PutAsJsonAsync(
            $"/api/pages/{DbSeeder.EngineeringId}/share",
            new { visibility = "Public", permission = "View" });
        Assert.Equal(HttpStatusCode.OK, shareResponse.StatusCode);
        var share = await shareResponse.Content.ReadFromJsonAsync<ShareResponse>(TestJson.Options);
        Assert.Equal(PageVisibility.Public, share!.Visibility);
        Assert.False(string.IsNullOrWhiteSpace(share.PublicSlug));

        var publicGet = await _client.GetAsync($"/api/public/pages/{share.PublicSlug}");
        Assert.Equal(HttpStatusCode.OK, publicGet.StatusCode);

        // Revoke: back to workspace-only clears the slug and 404s the public link.
        await _client.PutAsJsonAsync(
            $"/api/pages/{DbSeeder.EngineeringId}/share",
            new { visibility = "Workspace", permission = "Edit" });
        var revoked = await _client.GetAsync($"/api/public/pages/{share.PublicSlug}");
        Assert.Equal(HttpStatusCode.NotFound, revoked.StatusCode);
    }

    [Fact]
    public async Task Seeded_public_page_is_reachable_by_slug()
    {
        var response = await _client.GetAsync("/api/public/pages/acme-product-roadmap");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PageDetailResponse>(TestJson.Options);
        Assert.Equal(DbSeeder.ProductId, page!.Id);
    }

    // ---- favorites ----

    [Fact]
    public async Task Favorite_and_unfavorite_updates_list_and_tree()
    {
        var seeded = await _client.GetFromJsonAsync<List<FavoriteResponse>>(
            $"/api/workspaces/{WorkspaceId}/favorites");
        Assert.Contains(seeded!, f => f.Id == DbSeeder.GettingStartedId);

        await _client.PostAsync($"/api/pages/{DbSeeder.EngineeringId}/favorite", null);

        var favorites = await _client.GetFromJsonAsync<List<FavoriteResponse>>(
            $"/api/workspaces/{WorkspaceId}/favorites");
        Assert.Contains(favorites!, f => f.Id == DbSeeder.EngineeringId);

        var tree = await TreeAsync();
        Assert.True(tree.First(n => n.Id == DbSeeder.EngineeringId).IsFavorite);

        await _client.DeleteAsync($"/api/pages/{DbSeeder.EngineeringId}/favorite");
        var after = await _client.GetFromJsonAsync<List<FavoriteResponse>>(
            $"/api/workspaces/{WorkspaceId}/favorites");
        Assert.DoesNotContain(after!, f => f.Id == DbSeeder.EngineeringId);
    }

    // ---- search ----

    [Fact]
    public async Task Search_matches_page_title()
    {
        var results = await _client.GetFromJsonAsync<List<SearchResultResponse>>(
            $"/api/workspaces/{WorkspaceId}/search?q=Installation");

        var hit = Assert.Single(results!, r => r.PageId == DbSeeder.InstallationId);
        Assert.True(hit.MatchedTitle);
    }

    [Fact]
    public async Task Search_matches_block_text_with_snippet()
    {
        var results = await _client.GetFromJsonAsync<List<SearchResultResponse>>(
            $"/api/workspaces/{WorkspaceId}/search?q=Notion-style");

        var hit = Assert.Single(results!, r => r.PageId == DbSeeder.GettingStartedId);
        Assert.False(hit.MatchedTitle);
        Assert.Contains("Notion-style", hit.Snippet);
    }

    [Fact]
    public async Task Search_with_empty_query_returns_empty()
    {
        var results = await _client.GetFromJsonAsync<List<SearchResultResponse>>(
            $"/api/workspaces/{WorkspaceId}/search?q=");
        Assert.Empty(results!);
    }
}
