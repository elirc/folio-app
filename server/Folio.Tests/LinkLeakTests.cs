using System.Net.Http.Json;
using Folio.Api.Contracts;
using Folio.Infrastructure.Persistence;

namespace Folio.Tests;

/// <summary>
/// Inline-link privacy + rendering: an outgoing link to a page the reader can't
/// see must not leak that page's current title, and Markdown export flattens link
/// tokens to plain bracketed text.
/// </summary>
public class LinkLeakTests : IDisposable
{
    private readonly FolioApiFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private static async Task AddLinkAsync(HttpClient client, Guid sourcePage, Guid targetPage, string targetTitle)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/pages/{sourcePage}/blocks",
            new { type = "Paragraph", content = new { text = $"see #[{targetTitle}]({targetPage})" } });
        response.EnsureSuccessStatusCode();
    }

    private static Task<List<OutgoingLinkResponse>> OutgoingAsync(HttpClient client, Guid pageId) =>
        client.GetFromJsonAsync<List<OutgoingLinkResponse>>($"/api/pages/{pageId}/links", TestJson.Options)!;

    [Fact]
    public async Task Outgoing_link_to_a_page_the_reader_cannot_see_does_not_leak_its_current_title()
    {
        const string secret = "TOP-SECRET-CODENAME";
        var owner = _factory.CreateAuthenticatedClient(DbSeeder.OwnerEmail);

        // A workspace-visible page (Getting Started) links to Architecture, then
        // the owner renames Architecture to a secret and makes it private.
        await AddLinkAsync(owner, DbSeeder.GettingStartedId, DbSeeder.ArchitectureId, "Architecture");
        await owner.PutAsJsonAsync($"/api/pages/{DbSeeder.ArchitectureId}", new { title = secret, icon = (string?)null });
        await owner.PutAsJsonAsync(
            $"/api/pages/{DbSeeder.ArchitectureId}/share",
            new { visibility = "Private", permission = "View" });

        // The owner (who can see the private page) still gets the live title.
        var ownerLinks = await OutgoingAsync(owner, DbSeeder.GettingStartedId);
        var ownerLink = Assert.Single(ownerLinks);
        Assert.False(ownerLink.IsBroken);
        Assert.Equal(secret, ownerLink.TargetTitle);

        // A viewer must NOT see the private page's current title. The link is
        // reported broken with only the token title the source block already shows.
        var viewer = _factory.CreateAuthenticatedClient(DbSeeder.ViewerEmail);
        var viewerLinks = await OutgoingAsync(viewer, DbSeeder.GettingStartedId);
        var viewerLink = Assert.Single(viewerLinks);
        Assert.True(viewerLink.IsBroken);
        Assert.NotEqual(secret, viewerLink.TargetTitle);
        Assert.Equal("Architecture", viewerLink.TargetTitle);
    }

    [Fact]
    public async Task Backlinks_from_a_private_source_are_hidden_from_non_owners()
    {
        var owner = _factory.CreateAuthenticatedClient(DbSeeder.OwnerEmail);

        // Engineering (which a viewer can see) links to Product...
        await AddLinkAsync(owner, DbSeeder.EngineeringId, DbSeeder.ProductId, "Product");
        // ...then Engineering is made private (owner-only).
        await owner.PutAsJsonAsync(
            $"/api/pages/{DbSeeder.EngineeringId}/share",
            new { visibility = "Private", permission = "View" });

        // The owner sees the backlink; the viewer does not (private source hidden).
        var ownerBacklinks = await owner.GetFromJsonAsync<List<BacklinkResponse>>(
            $"/api/pages/{DbSeeder.ProductId}/backlinks", TestJson.Options);
        Assert.Contains(ownerBacklinks!, b => b.SourcePageId == DbSeeder.EngineeringId);

        var viewer = _factory.CreateAuthenticatedClient(DbSeeder.ViewerEmail);
        var viewerBacklinks = await viewer.GetFromJsonAsync<List<BacklinkResponse>>(
            $"/api/pages/{DbSeeder.ProductId}/backlinks", TestJson.Options);
        Assert.DoesNotContain(viewerBacklinks!, b => b.SourcePageId == DbSeeder.EngineeringId);
    }

    [Fact]
    public async Task Broken_link_heals_across_delete_then_restore()
    {
        var owner = _factory.CreateAuthenticatedClient(DbSeeder.OwnerEmail);
        await AddLinkAsync(owner, DbSeeder.EngineeringId, DbSeeder.ArchitectureId, "Architecture");

        await owner.DeleteAsync($"/api/pages/{DbSeeder.ArchitectureId}");
        Assert.True(Assert.Single(await OutgoingAsync(owner, DbSeeder.EngineeringId)).IsBroken);

        await owner.PostAsync($"/api/pages/{DbSeeder.ArchitectureId}/restore", null);
        Assert.False(Assert.Single(await OutgoingAsync(owner, DbSeeder.EngineeringId)).IsBroken);
    }

    [Fact]
    public async Task Export_renders_link_tokens_as_plain_bracketed_text()
    {
        var owner = _factory.CreateAuthenticatedClient(DbSeeder.OwnerEmail);
        await AddLinkAsync(owner, DbSeeder.EngineeringId, DbSeeder.ArchitectureId, "Architecture");

        var export = await owner.GetFromJsonAsync<ExportResponse>(
            $"/api/pages/{DbSeeder.EngineeringId}/export", TestJson.Options);

        // The #[Title](guid) token is flattened to [Title]; the raw token and guid
        // never appear in the exported Markdown.
        Assert.Contains("[Architecture]", export!.Markdown);
        Assert.DoesNotContain("#[", export.Markdown);
        Assert.DoesNotContain(DbSeeder.ArchitectureId.ToString(), export.Markdown);
    }
}
