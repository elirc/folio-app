using System.Net.Http.Json;
using Folio.Api.Contracts;
using Folio.Infrastructure.Persistence;

namespace Folio.Tests;

/// <summary>Search filters (author, favorites, date range) and quick-open ranking.</summary>
public class SearchV2Tests : IDisposable
{
    private static readonly Guid WorkspaceId = DbSeeder.WorkspaceId;
    private readonly FolioApiFactory _factory = new();
    private readonly HttpClient _client;

    public SearchV2Tests() => _client = _factory.CreateAuthenticatedClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private Task<List<SearchResultResponse>> SearchAsync(string queryString) =>
        _client.GetFromJsonAsync<List<SearchResultResponse>>(
            $"/api/workspaces/{WorkspaceId}/search?{queryString}", TestJson.Options)!;

    private Task<List<QuickOpenResult>> QuickOpenAsync(string q) =>
        _client.GetFromJsonAsync<List<QuickOpenResult>>(
            $"/api/workspaces/{WorkspaceId}/quick-open?q={q}", TestJson.Options)!;

    [Fact]
    public async Task Favorites_filter_returns_only_favorited_pages()
    {
        // Only Getting Started is seeded as a favorite.
        var results = await SearchAsync("favorites=true");

        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal(DbSeeder.GettingStartedId, r.PageId));
    }

    [Fact]
    public async Task Author_filter_narrows_to_pages_by_that_member()
    {
        var results = await SearchAsync($"author={DbSeeder.OwnerMemberId}&favorites=true");

        Assert.Contains(results, r => r.PageId == DbSeeder.GettingStartedId);
    }

    [Fact]
    public async Task Date_range_filter_excludes_out_of_range_pages()
    {
        // Everything is seeded at 2026-01-01; a future lower bound yields nothing.
        var results = await SearchAsync("updatedAfter=2030-01-01T00:00:00Z");
        Assert.Empty(results);
    }

    [Fact]
    public async Task Term_search_still_matches_and_reports_updatedAt()
    {
        var results = await SearchAsync("q=Installation");

        var hit = Assert.Single(results, r => r.PageId == DbSeeder.InstallationId);
        Assert.True(hit.MatchedTitle);
        Assert.NotEqual(default, hit.UpdatedAt);
    }

    [Fact]
    public async Task QuickOpen_ranks_title_prefix_matches_first()
    {
        // Both "Installation" and (via contains) any page with "ation".
        var results = await QuickOpenAsync("Inst");

        Assert.NotEmpty(results);
        Assert.Equal(DbSeeder.InstallationId, results[0].PageId);
    }

    [Fact]
    public async Task QuickOpen_with_empty_query_returns_recent_pages()
    {
        var results = await QuickOpenAsync("");

        Assert.NotEmpty(results);
        Assert.True(results.Count <= 10);
    }

    [Fact]
    public async Task QuickOpen_respects_visibility_for_non_owners()
    {
        // Make a page private as owner, then a viewer must not quick-open to it.
        await _client.PutAsJsonAsync(
            $"/api/pages/{DbSeeder.EngineeringId}/share",
            new { visibility = "Private", permission = "View" });

        var viewer = _factory.CreateAuthenticatedClient(DbSeeder.ViewerEmail);
        var results = await viewer.GetFromJsonAsync<List<QuickOpenResult>>(
            $"/api/workspaces/{WorkspaceId}/quick-open?q=Engineering", TestJson.Options);

        Assert.DoesNotContain(results!, r => r.PageId == DbSeeder.EngineeringId);
    }
}
