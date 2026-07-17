using System.Net;
using System.Net.Http.Json;
using Folio.Api.Contracts;
using Folio.Infrastructure.Persistence;

namespace Folio.Tests;

/// <summary>
/// Page CRUD + tree operations. Each test gets a fresh in-memory database
/// (new factory per test) so mutations never leak between cases.
/// </summary>
public class PageEndpointTests : IDisposable
{
    private static readonly Guid WorkspaceId = DbSeeder.WorkspaceId;
    private readonly FolioApiFactory _factory = new();
    private readonly HttpClient _client;

    public PageEndpointTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Tree_returns_seeded_roots_and_children()
    {
        var tree = await _client.GetFromJsonAsync<List<PageTreeNode>>(
            $"/api/workspaces/{WorkspaceId}/pages/tree");

        Assert.NotNull(tree);
        Assert.Equal(["Getting Started", "Engineering", "Product"], tree!.Select(n => n.Title));
        var gettingStarted = tree.First(n => n.Title == "Getting Started");
        Assert.Equal(["Installation", "Configuration"], gettingStarted.Children.Select(c => c.Title));
    }

    [Fact]
    public async Task Create_root_page_appends_at_end()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/workspaces/{WorkspaceId}/pages",
            new { title = "Marketing" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<PageDetailResponse>();
        Assert.NotNull(created);
        Assert.Null(created!.ParentId);
        Assert.Equal(3, created.Position); // after the 3 seeded roots
        Assert.Single(created.Breadcrumb);
    }

    [Fact]
    public async Task Create_child_page_nests_under_parent()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/workspaces/{WorkspaceId}/pages",
            new { title = "FAQ", parentId = DbSeeder.GettingStartedId });

        var created = await response.Content.ReadFromJsonAsync<PageDetailResponse>();
        Assert.Equal(DbSeeder.GettingStartedId, created!.ParentId);
        Assert.Equal(2, created.Position); // appended after Installation, Configuration
        Assert.Equal(["Getting Started", "FAQ"], created.Breadcrumb.Select(b => b.Title));
    }

    [Fact]
    public async Task Create_with_missing_title_returns_400()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/workspaces/{WorkspaceId}/pages",
            new { parentId = (Guid?)null });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Rename_updates_title()
    {
        var response = await _client.PutAsJsonAsync(
            $"/api/pages/{DbSeeder.ProductId}",
            new { title = "Product & Design", icon = "🎨" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<PageDetailResponse>();
        Assert.Equal("Product & Design", updated!.Title);
        Assert.Equal("🎨", updated.Icon);
    }

    [Fact]
    public async Task Move_reparents_and_reorders_siblings()
    {
        // Move "Installation" out of "Getting Started" to a root at position 0.
        var response = await _client.PostAsJsonAsync(
            $"/api/pages/{DbSeeder.InstallationId}/move",
            new { parentId = (Guid?)null, position = 0 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tree = await _client.GetFromJsonAsync<List<PageTreeNode>>(
            $"/api/workspaces/{WorkspaceId}/pages/tree");
        Assert.Equal("Installation", tree![0].Title);

        // "Configuration" is now the only remaining child, reindexed to 0.
        var gettingStarted = tree.First(n => n.Title == "Getting Started");
        var configuration = Assert.Single(gettingStarted.Children);
        Assert.Equal("Configuration", configuration.Title);
        Assert.Equal(0, configuration.Position);
    }

    [Fact]
    public async Task Move_into_own_descendant_is_rejected()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/pages/{DbSeeder.GettingStartedId}/move",
            new { parentId = DbSeeder.InstallationId, position = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Delete_removes_page_and_its_subtree()
    {
        var response = await _client.DeleteAsync($"/api/pages/{DbSeeder.GettingStartedId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Parent and both children are gone.
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync($"/api/pages/{DbSeeder.GettingStartedId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync($"/api/pages/{DbSeeder.InstallationId}")).StatusCode);

        var tree = await _client.GetFromJsonAsync<List<PageTreeNode>>(
            $"/api/workspaces/{WorkspaceId}/pages/tree");
        Assert.Equal(["Engineering", "Product"], tree!.Select(n => n.Title));
        Assert.Equal([0, 1], tree.Select(n => n.Position));
    }

    [Fact]
    public async Task Breadcrumb_endpoint_returns_ancestor_chain()
    {
        var trail = await _client.GetFromJsonAsync<List<BreadcrumbItem>>(
            $"/api/pages/{DbSeeder.InstallationId}/breadcrumb");

        Assert.Equal(["Getting Started", "Installation"], trail!.Select(b => b.Title));
    }
}
