using System.Net.Http.Json;
using Folio.Api.Contracts;
using Folio.Infrastructure.Persistence;

namespace Folio.Tests;

/// <summary>Inline page links: backlinks, and broken-link handling on delete/restore.</summary>
public class LinkTests : IDisposable
{
    private readonly FolioApiFactory _factory = new();
    private readonly HttpClient _client;

    public LinkTests() => _client = _factory.CreateAuthenticatedClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    // Adds a block on `sourcePage` that links to `targetPage`.
    private async Task AddLinkingBlockAsync(Guid sourcePage, Guid targetPage, string targetTitle)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/pages/{sourcePage}/blocks",
            new { type = "Paragraph", content = new { text = $"see #[{targetTitle}]({targetPage})" } });
        response.EnsureSuccessStatusCode();
    }

    private Task<List<BacklinkResponse>> BacklinksAsync(Guid pageId) =>
        _client.GetFromJsonAsync<List<BacklinkResponse>>($"/api/pages/{pageId}/backlinks", TestJson.Options)!;

    private Task<List<OutgoingLinkResponse>> OutgoingAsync(Guid pageId) =>
        _client.GetFromJsonAsync<List<OutgoingLinkResponse>>($"/api/pages/{pageId}/links", TestJson.Options)!;

    [Fact]
    public async Task Linking_block_creates_a_backlink()
    {
        await AddLinkingBlockAsync(DbSeeder.EngineeringId, DbSeeder.ArchitectureId, "Architecture");

        var backlinks = await BacklinksAsync(DbSeeder.ArchitectureId);
        var hit = Assert.Single(backlinks);
        Assert.Equal(DbSeeder.EngineeringId, hit.SourcePageId);
        Assert.Equal("Engineering", hit.SourcePageTitle);
    }

    [Fact]
    public async Task Outgoing_links_are_reported()
    {
        await AddLinkingBlockAsync(DbSeeder.EngineeringId, DbSeeder.ArchitectureId, "Architecture");

        var outgoing = await OutgoingAsync(DbSeeder.EngineeringId);
        var link = Assert.Single(outgoing);
        Assert.Equal(DbSeeder.ArchitectureId, link.TargetPageId);
        Assert.False(link.IsBroken);
    }

    [Fact]
    public async Task Editing_a_block_resyncs_its_links()
    {
        await AddLinkingBlockAsync(DbSeeder.EngineeringId, DbSeeder.ArchitectureId, "Architecture");
        var blocks = await _client.GetFromJsonAsync<List<BlockResponse>>(
            $"/api/pages/{DbSeeder.EngineeringId}/blocks", TestJson.Options);
        var linkBlock = blocks!.First(b => b.Content.GetProperty("text").GetString()!.Contains("#["));

        // Remove the link by editing the text.
        await _client.PutAsJsonAsync(
            $"/api/blocks/{linkBlock.Id}",
            new { type = "Paragraph", content = new { text = "no link anymore" } });

        Assert.Empty(await BacklinksAsync(DbSeeder.ArchitectureId));
    }

    [Fact]
    public async Task Deleting_target_page_makes_outgoing_link_broken_and_restore_heals_it()
    {
        await AddLinkingBlockAsync(DbSeeder.EngineeringId, DbSeeder.ArchitectureId, "Architecture");

        // Trash the target page.
        await _client.DeleteAsync($"/api/pages/{DbSeeder.ArchitectureId}");

        var brokenLinks = await OutgoingAsync(DbSeeder.EngineeringId);
        Assert.True(Assert.Single(brokenLinks).IsBroken);

        // A trashed source page contributes no backlinks either.
        // Restore the target: the link heals.
        await _client.PostAsync($"/api/pages/{DbSeeder.ArchitectureId}/restore", null);
        var healed = await OutgoingAsync(DbSeeder.EngineeringId);
        Assert.False(Assert.Single(healed).IsBroken);
    }

    [Fact]
    public async Task Deleting_source_block_removes_its_links()
    {
        await AddLinkingBlockAsync(DbSeeder.EngineeringId, DbSeeder.ArchitectureId, "Architecture");
        var blocks = await _client.GetFromJsonAsync<List<BlockResponse>>(
            $"/api/pages/{DbSeeder.EngineeringId}/blocks", TestJson.Options);
        var linkBlock = blocks!.First(b => b.Content.GetProperty("text").GetString()!.Contains("#["));

        await _client.DeleteAsync($"/api/blocks/{linkBlock.Id}");

        Assert.Empty(await BacklinksAsync(DbSeeder.ArchitectureId));
    }
}
