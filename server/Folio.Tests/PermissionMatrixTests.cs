using System.Net;
using System.Net.Http.Json;
using Folio.Api.Contracts;
using Folio.Infrastructure.Persistence;

namespace Folio.Tests;

/// <summary>
/// The full read/write access matrix: role (Owner/Editor/Viewer) × page visibility
/// (Private/Workspace/Public) × share permission (View/Edit), asserted against the
/// real page read and write endpoints. Also pins foreign-workspace 404s and
/// public-link behaviour when a shared page is later made private.
/// </summary>
public class PermissionMatrixTests
{
    private static readonly Guid WorkspaceId = DbSeeder.WorkspaceId;
    // Engineering is a Workspace-visibility root authored by the owner; a clean
    // page to re-share into each visibility under test.
    private static readonly Guid PageId = DbSeeder.EngineeringId;

    private static string Email(string role) => role switch
    {
        "Owner" => DbSeeder.OwnerEmail,
        "Editor" => DbSeeder.EditorEmail,
        _ => DbSeeder.ViewerEmail,
    };

    [Theory]
    // role,     visibility,  permission, expectedRead, expectedWrite
    [InlineData("Owner", "Private", "View", 200, 200)]
    [InlineData("Owner", "Workspace", "Edit", 200, 200)]
    [InlineData("Owner", "Public", "View", 200, 200)]
    [InlineData("Editor", "Private", "View", 403, 403)] // private is owner-only, even to read
    [InlineData("Editor", "Private", "Edit", 403, 403)]
    [InlineData("Editor", "Workspace", "View", 200, 403)] // reads, but View perm blocks writes
    [InlineData("Editor", "Workspace", "Edit", 200, 200)] // Edit perm unlocks writes
    [InlineData("Editor", "Public", "View", 200, 403)]
    [InlineData("Editor", "Public", "Edit", 200, 200)]
    [InlineData("Viewer", "Private", "View", 403, 403)]
    [InlineData("Viewer", "Workspace", "View", 200, 403)] // viewers read but never write
    [InlineData("Viewer", "Workspace", "Edit", 200, 403)]
    [InlineData("Viewer", "Public", "Edit", 200, 403)]
    public async Task Read_and_write_follow_the_permission_matrix(
        string role, string visibility, string permission, int expectedRead, int expectedWrite)
    {
        using var factory = new FolioApiFactory();

        // The owner configures the page's visibility + share permission.
        var owner = factory.CreateAuthenticatedClient(DbSeeder.OwnerEmail);
        var share = await owner.PutAsJsonAsync(
            $"/api/pages/{PageId}/share",
            new { visibility, permission });
        Assert.Equal(HttpStatusCode.OK, share.StatusCode);

        var client = factory.CreateAuthenticatedClient(Email(role));

        var read = await client.GetAsync($"/api/pages/{PageId}");
        Assert.Equal(expectedRead, (int)read.StatusCode);

        var write = await client.PutAsJsonAsync(
            $"/api/pages/{PageId}",
            new { title = $"{role} write attempt", icon = (string?)null });
        Assert.Equal(expectedWrite, (int)write.StatusCode);
    }

    [Theory]
    [InlineData("/api/pages/{0}")]
    [InlineData("/api/pages/{0}/blocks")]
    [InlineData("/api/pages/{0}/comments")]
    [InlineData("/api/pages/{0}/versions")]
    [InlineData("/api/pages/{0}/backlinks")]
    [InlineData("/api/pages/{0}/links")]
    [InlineData("/api/pages/{0}/export")]
    [InlineData("/api/pages/{0}/breadcrumb")]
    public async Task Foreign_workspace_resources_are_404_across_read_endpoints(string template)
    {
        using var factory = new FolioApiFactory();
        // Eve (Globex) must not distinguish Acme's pages from non-existent ones.
        var eve = factory.CreateAuthenticatedClient(DbSeeder.GlobexOwnerEmail);

        var response = await eve.GetAsync(string.Format(template, DbSeeder.GettingStartedId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Public_link_stops_resolving_once_the_page_is_made_private()
    {
        using var factory = new FolioApiFactory();
        var owner = factory.CreateAuthenticatedClient(DbSeeder.OwnerEmail);

        // Share Engineering publicly and confirm the anonymous link resolves.
        var shareResponse = await owner.PutAsJsonAsync(
            $"/api/pages/{PageId}/share",
            new { visibility = "Public", permission = "View" });
        var share = await shareResponse.Content.ReadFromJsonAsync<ShareResponse>(TestJson.Options);
        var slug = share!.PublicSlug!;

        var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync($"/api/public/pages/{slug}")).StatusCode);

        // Move it to Private: the public link is revoked (slug cleared) → 404.
        await owner.PutAsJsonAsync(
            $"/api/pages/{PageId}/share",
            new { visibility = "Private", permission = "View" });

        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/public/pages/{slug}")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_caller_cannot_reach_authorized_endpoints()
    {
        using var factory = new FolioApiFactory();
        var anon = factory.CreateClient();

        // No bearer token → 401 before any authorization logic runs.
        var response = await anon.GetAsync($"/api/pages/{DbSeeder.GettingStartedId}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
