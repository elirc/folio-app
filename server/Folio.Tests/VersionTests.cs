using System.Net;
using System.Net.Http.Json;
using Folio.Api.Contracts;
using Folio.Domain.Enums;
using Folio.Infrastructure.Persistence;

namespace Folio.Tests;

/// <summary>Page history: snapshot on save, list, diff summary, non-destructive restore.</summary>
public class VersionTests : IDisposable
{
    private static readonly Guid PageId = DbSeeder.GettingStartedId;
    private readonly FolioApiFactory _factory = new();
    private readonly HttpClient _client;

    public VersionTests() => _client = _factory.CreateAuthenticatedClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<VersionSummaryResponse> SnapshotAsync()
    {
        var response = await _client.PostAsync($"/api/pages/{PageId}/versions", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<VersionSummaryResponse>(TestJson.Options))!;
    }

    private Task<List<VersionSummaryResponse>> ListAsync() =>
        _client.GetFromJsonAsync<List<VersionSummaryResponse>>($"/api/pages/{PageId}/versions", TestJson.Options)!;

    [Fact]
    public async Task Snapshot_captures_title_and_block_count()
    {
        var version = await SnapshotAsync();

        Assert.Equal(1, version.VersionNumber);
        Assert.Equal("Getting Started", version.Title);
        Assert.Equal(5, version.BlockCount); // seeded blocks on the page
        Assert.Equal("Ada Lovelace", version.CreatedByName);
    }

    [Fact]
    public async Task Versions_are_numbered_and_listed_newest_first()
    {
        await SnapshotAsync();
        await SnapshotAsync();

        var list = await ListAsync();
        Assert.Equal([2, 1], list.Select(v => v.VersionNumber));
    }

    [Fact]
    public async Task Diff_summary_counts_added_and_changed_since_a_version()
    {
        var v1 = await SnapshotAsync();

        // Add a block and change the title after the snapshot.
        await _client.PostAsJsonAsync(
            $"/api/pages/{PageId}/blocks",
            new { type = "Paragraph", content = new { text = "brand new" } });
        var blocks = await _client.GetFromJsonAsync<List<BlockResponse>>($"/api/pages/{PageId}/blocks", TestJson.Options);
        await _client.PutAsJsonAsync(
            $"/api/blocks/{blocks![0].Id}",
            new { type = "Heading", content = new { text = "Changed heading", level = 1 } });

        var detail = await _client.GetFromJsonAsync<VersionDetailResponse>(
            $"/api/pages/{PageId}/versions/{v1.VersionNumber}", TestJson.Options);

        Assert.Equal(1, detail!.Diff.Added);   // the new paragraph
        Assert.Equal(1, detail.Diff.Changed);  // the edited heading
        Assert.Equal(0, detail.Diff.Removed);
    }

    [Fact]
    public async Task Restore_reverts_content_and_is_non_destructive()
    {
        // Snapshot the seeded state (v1), then mutate the page heavily.
        var v1 = await SnapshotAsync();
        await _client.PutAsJsonAsync($"/api/pages/{PageId}", new { title = "Totally Different", icon = (string?)null });
        var blocks = await _client.GetFromJsonAsync<List<BlockResponse>>($"/api/pages/{PageId}/blocks", TestJson.Options);
        await _client.DeleteAsync($"/api/blocks/{blocks![0].Id}");

        // Restore v1.
        var restore = await _client.PostAsync($"/api/pages/{PageId}/versions/{v1.VersionNumber}/restore", null);
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);

        // Title + block count are back to the v1 snapshot.
        var page = await _client.GetFromJsonAsync<PageDetailResponse>($"/api/pages/{PageId}", TestJson.Options);
        Assert.Equal("Getting Started", page!.Title);
        var restoredBlocks = await _client.GetFromJsonAsync<List<BlockResponse>>(
            $"/api/pages/{PageId}/blocks", TestJson.Options);
        Assert.Equal(5, restoredBlocks!.Count);

        // Non-destructive: v1 still exists and a new pre-restore version was added.
        var list = await ListAsync();
        Assert.Contains(list, v => v.VersionNumber == 1);
        Assert.True(list.Count >= 2);
        Assert.Contains(list, v => v.Label != null && v.Label.Contains("restore"));
    }

    [Fact]
    public async Task Viewer_cannot_snapshot_but_can_list()
    {
        var owner = _factory.CreateAuthenticatedClient(DbSeeder.OwnerEmail);
        await owner.PostAsync($"/api/pages/{PageId}/versions", null); // create v1 as owner

        var viewer = _factory.CreateAuthenticatedClient(DbSeeder.ViewerEmail);
        var snapshot = await viewer.PostAsync($"/api/pages/{PageId}/versions", null);
        Assert.Equal(HttpStatusCode.Forbidden, snapshot.StatusCode);

        var list = await viewer.GetAsync($"/api/pages/{PageId}/versions");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
    }
}
