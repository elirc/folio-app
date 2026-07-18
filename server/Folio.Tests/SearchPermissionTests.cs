using System.Net.Http.Json;
using Folio.Api.Contracts;
using Folio.Infrastructure.Persistence;

namespace Folio.Tests;

/// <summary>
/// Search + quick-open: results never include pages the caller can't see, filter
/// combinations compose, empty queries/no-matches return empty, and ranking is
/// stable (title-prefix first).
/// </summary>
public class SearchPermissionTests : IDisposable
{
    private static readonly Guid WorkspaceId = DbSeeder.WorkspaceId;
    private readonly FolioApiFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private static Task<List<SearchResultResponse>> SearchAsync(HttpClient client, string queryString) =>
        client.GetFromJsonAsync<List<SearchResultResponse>>(
            $"/api/workspaces/{WorkspaceId}/search?{queryString}", TestJson.Options)!;

    private static Task<List<QuickOpenResult>> QuickOpenAsync(HttpClient client, string q) =>
        client.GetFromJsonAsync<List<QuickOpenResult>>(
            $"/api/workspaces/{WorkspaceId}/quick-open?q={Uri.EscapeDataString(q)}", TestJson.Options)!;

    [Fact]
    public async Task Search_never_returns_a_page_the_caller_cannot_see()
    {
        var owner = _factory.CreateAuthenticatedClient(DbSeeder.OwnerEmail);

        // Give Engineering a distinctive title, then hide it (private = owner-only).
        await owner.PutAsJsonAsync($"/api/pages/{DbSeeder.EngineeringId}", new { title = "ZebraSecret", icon = (string?)null });
        await owner.PutAsJsonAsync(
            $"/api/pages/{DbSeeder.EngineeringId}/share",
            new { visibility = "Private", permission = "View" });

        // The owner finds it; a viewer gets nothing (the private page is filtered out).
        var ownerHits = await SearchAsync(owner, "q=ZebraSecret");
        Assert.Contains(ownerHits, r => r.PageId == DbSeeder.EngineeringId);

        var viewer = _factory.CreateAuthenticatedClient(DbSeeder.ViewerEmail);
        var viewerHits = await SearchAsync(viewer, "q=ZebraSecret");
        Assert.Empty(viewerHits);
    }

    [Fact]
    public async Task Filter_combinations_compose()
    {
        var owner = _factory.CreateAuthenticatedClient(DbSeeder.OwnerEmail);

        // author + favorites + term: Getting Started is owner-authored, favorited,
        // and title-matches "Getting".
        var combined = await SearchAsync(owner, $"q=Getting&author={DbSeeder.OwnerMemberId}&favorites=true");
        Assert.Contains(combined, r => r.PageId == DbSeeder.GettingStartedId);

        // A different author with favorites → nothing (all seeded pages are Ada's).
        var otherAuthor = await SearchAsync(owner, $"author={DbSeeder.EditorMemberId}&favorites=true");
        Assert.Empty(otherAuthor);

        // favorites + an impossible date window → nothing.
        var impossibleWindow = await SearchAsync(owner, "favorites=true&updatedBefore=2000-01-01T00:00:00Z");
        Assert.Empty(impossibleWindow);
    }

    [Fact]
    public async Task Empty_query_and_no_match_query_both_return_empty()
    {
        var owner = _factory.CreateAuthenticatedClient(DbSeeder.OwnerEmail);

        // No term and no filters → empty (search requires at least one criterion).
        Assert.Empty(await SearchAsync(owner, "q="));
        // A term with no matches → empty.
        Assert.Empty(await SearchAsync(owner, "q=NoSuchTermQZX"));
    }

    [Fact]
    public async Task QuickOpen_ranks_prefix_matches_ahead_of_contains_matches_stably()
    {
        var owner = _factory.CreateAuthenticatedClient(DbSeeder.OwnerEmail);

        // "Xylophone" prefix-matches "Xylo"; "My Xylo Notes" only contains it.
        await owner.PostAsJsonAsync($"/api/workspaces/{WorkspaceId}/pages", new { title = "My Xylo Notes" });
        await owner.PostAsJsonAsync($"/api/workspaces/{WorkspaceId}/pages", new { title = "Xylophone" });

        // Ranking is deterministic across repeated calls: prefix match first.
        for (var i = 0; i < 3; i++)
        {
            var results = await QuickOpenAsync(owner, "Xylo");
            Assert.Equal("Xylophone", results[0].Title);
            Assert.Contains(results, r => r.Title == "My Xylo Notes");
        }
    }

    [Fact]
    public async Task QuickOpen_excludes_private_pages_for_non_owners()
    {
        var owner = _factory.CreateAuthenticatedClient(DbSeeder.OwnerEmail);
        await owner.PutAsJsonAsync(
            $"/api/pages/{DbSeeder.EngineeringId}/share",
            new { visibility = "Private", permission = "View" });

        var viewer = _factory.CreateAuthenticatedClient(DbSeeder.ViewerEmail);
        var results = await QuickOpenAsync(viewer, "Engineering");
        Assert.DoesNotContain(results, r => r.PageId == DbSeeder.EngineeringId);
    }
}
