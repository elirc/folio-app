using System.Net;
using System.Net.Http.Json;
using Folio.Api.Contracts;
using Folio.Domain.Enums;
using Folio.Infrastructure.Persistence;

namespace Folio.Tests;

/// <summary>
/// JWT login and the page-permission matrix: foreign workspace → 404, insufficient
/// permission → 403, private pages owner-only, public links readable anonymously.
/// Each test gets a fresh in-memory database.
/// </summary>
public class AuthTests : IDisposable
{
    private static readonly Guid WorkspaceId = DbSeeder.WorkspaceId;
    private readonly FolioApiFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<HttpResponseMessage> Login(string email, string password) =>
        await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password });

    // ---- login ----

    [Fact]
    public async Task Login_with_valid_credentials_returns_token_and_member()
    {
        var response = await Login(DbSeeder.OwnerEmail, DbSeeder.DefaultPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(TestJson.Options);
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.Equal(MemberRole.Owner, body.Member.Role);
        Assert.Equal(WorkspaceId, body.Member.WorkspaceId);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        var response = await Login(DbSeeder.OwnerEmail, "wrong");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_unknown_email_returns_401()
    {
        var response = await Login("nobody@acme.test", DbSeeder.DefaultPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoint_without_token_returns_401()
    {
        var anon = _factory.CreateClient();
        var response = await anon.GetAsync($"/api/workspaces/{WorkspaceId}/pages/tree");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_returns_current_member()
    {
        var client = _factory.CreateAuthenticatedClient(DbSeeder.EditorEmail);
        var me = await client.GetFromJsonAsync<MemberResponse>("/api/auth/me", TestJson.Options);
        Assert.Equal(DbSeeder.EditorEmail, me!.Email);
        Assert.Equal(MemberRole.Editor, me.Role);
    }

    // ---- cross-workspace isolation (foreign → 404) ----

    [Fact]
    public async Task Foreign_workspace_page_is_404_not_403()
    {
        // Eve is in Globex; Acme's seeded pages must look non-existent to her.
        var eve = _factory.CreateAuthenticatedClient(DbSeeder.GlobexOwnerEmail);

        var page = await eve.GetAsync($"/api/pages/{DbSeeder.GettingStartedId}");
        Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);

        var tree = await eve.GetAsync($"/api/workspaces/{WorkspaceId}/pages/tree");
        Assert.Equal(HttpStatusCode.NotFound, tree.StatusCode);
    }

    // ---- private pages are owner-only ----

    [Fact]
    public async Task Private_page_is_403_for_non_owner_members()
    {
        var owner = _factory.CreateAuthenticatedClient(DbSeeder.OwnerEmail);
        await owner.PutAsJsonAsync(
            $"/api/pages/{DbSeeder.EngineeringId}/share",
            new { visibility = "Private", permission = "View" });

        var editor = _factory.CreateAuthenticatedClient(DbSeeder.EditorEmail);
        var editorGet = await editor.GetAsync($"/api/pages/{DbSeeder.EngineeringId}");
        Assert.Equal(HttpStatusCode.Forbidden, editorGet.StatusCode);

        // The owner still sees it.
        var ownerGet = await owner.GetAsync($"/api/pages/{DbSeeder.EngineeringId}");
        Assert.Equal(HttpStatusCode.OK, ownerGet.StatusCode);
    }

    // ---- write permission by role + share permission ----

    [Fact]
    public async Task Viewer_cannot_write_workspace_page_403()
    {
        var viewer = _factory.CreateAuthenticatedClient(DbSeeder.ViewerEmail);

        var rename = await viewer.PutAsJsonAsync(
            $"/api/pages/{DbSeeder.GettingStartedId}",
            new { title = "Nope", icon = (string?)null });
        Assert.Equal(HttpStatusCode.Forbidden, rename.StatusCode);

        // But a Viewer may still read workspace pages.
        var read = await viewer.GetAsync($"/api/pages/{DbSeeder.GettingStartedId}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
    }

    [Fact]
    public async Task Editor_write_depends_on_page_share_permission()
    {
        var owner = _factory.CreateAuthenticatedClient(DbSeeder.OwnerEmail);
        var editor = _factory.CreateAuthenticatedClient(DbSeeder.EditorEmail);

        // Seeded pages are View-only: an Editor cannot write yet.
        var blocked = await editor.PutAsJsonAsync(
            $"/api/pages/{DbSeeder.EngineeringId}",
            new { title = "Editor edit", icon = (string?)null });
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);

        // Owner grants Edit; now the Editor can write.
        await owner.PutAsJsonAsync(
            $"/api/pages/{DbSeeder.EngineeringId}/share",
            new { visibility = "Workspace", permission = "Edit" });

        var allowed = await editor.PutAsJsonAsync(
            $"/api/pages/{DbSeeder.EngineeringId}",
            new { title = "Editor edit", icon = (string?)null });
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    // ---- public link readable without auth ----

    [Fact]
    public async Task Public_link_page_is_readable_without_a_token()
    {
        var anon = _factory.CreateClient();
        var response = await anon.GetAsync("/api/public/pages/acme-product-roadmap");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
