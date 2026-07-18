using System.Net;
using System.Net.Http.Json;
using Folio.Api.Contracts;
using Folio.Domain.Enums;
using Folio.Infrastructure.Persistence;

namespace Folio.Tests;

/// <summary>
/// Page-history edge cases: restoring a version that was itself produced by a
/// restore, diffs driven purely by a block type change, and snapshot/restore of a
/// page whose blocks are nested under a toggle.
/// </summary>
public class HistoryEdgeTests : IDisposable
{
    private readonly FolioApiFactory _factory = new();
    private readonly HttpClient _client;

    public HistoryEdgeTests() => _client = _factory.CreateAuthenticatedClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<VersionSummaryResponse> SnapshotAsync(Guid pageId)
    {
        var response = await _client.PostAsync($"/api/pages/{pageId}/versions", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<VersionSummaryResponse>(TestJson.Options))!;
    }

    private Task<List<BlockResponse>> BlocksAsync(Guid pageId) =>
        _client.GetFromJsonAsync<List<BlockResponse>>($"/api/pages/{pageId}/blocks", TestJson.Options)!;

    [Fact]
    public async Task Restore_of_a_restore_round_trips_to_the_intermediate_state()
    {
        var pageId = DbSeeder.GettingStartedId;

        // v1 = seeded state (5 blocks, title "Getting Started").
        var v1 = await SnapshotAsync(pageId);

        // Mutate: rename + drop a block, then restore v1. That pre-snapshots the
        // mutated "4-block / renamed" state as v2 and reverts the page to v1.
        await _client.PutAsJsonAsync($"/api/pages/{pageId}", new { title = "Mutated", icon = (string?)null });
        var blocks = await BlocksAsync(pageId);
        await _client.DeleteAsync($"/api/blocks/{blocks[0].Id}");
        await _client.PostAsync($"/api/pages/{pageId}/versions/{v1.VersionNumber}/restore", null);

        // The restore's pre-snapshot (v2) captured the mutated state.
        var versions = await _client.GetFromJsonAsync<List<VersionSummaryResponse>>(
            $"/api/pages/{pageId}/versions", TestJson.Options);
        var v2 = versions!.First(v => v.Label != null && v.Label.Contains("restore"));
        Assert.Equal(4, v2.BlockCount);
        Assert.Equal("Mutated", v2.Title);

        // Now restore that restore-produced version: the page returns to the
        // intermediate 4-block / "Mutated" state.
        var restore = await _client.PostAsync($"/api/pages/{pageId}/versions/{v2.VersionNumber}/restore", null);
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);

        var page = await _client.GetFromJsonAsync<PageDetailResponse>($"/api/pages/{pageId}", TestJson.Options);
        Assert.Equal("Mutated", page!.Title);
        Assert.Equal(4, (await BlocksAsync(pageId)).Count);
    }

    [Fact]
    public async Task Diff_counts_a_pure_block_type_change_as_changed()
    {
        var pageId = DbSeeder.GettingStartedId;
        var blocks = await BlocksAsync(pageId);
        var paragraph = blocks.First(b => b.Type == BlockType.Paragraph);
        var text = paragraph.Content.GetProperty("text").GetString();

        var v1 = await SnapshotAsync(pageId);

        // Change only the block's type (same text) → the diff should flag it changed,
        // with nothing added or removed.
        await _client.PutAsJsonAsync(
            $"/api/blocks/{paragraph.Id}",
            new { type = "Quote", content = new { text } });

        var detail = await _client.GetFromJsonAsync<VersionDetailResponse>(
            $"/api/pages/{pageId}/versions/{v1.VersionNumber}", TestJson.Options);

        Assert.Equal(0, detail!.Diff.Added);
        Assert.Equal(0, detail.Diff.Removed);
        Assert.Equal(1, detail.Diff.Changed);
    }

    [Fact]
    public async Task Snapshot_and_restore_preserve_nested_toggle_children()
    {
        // Configuration is seeded with a Toggle that has two nested children.
        var pageId = DbSeeder.ConfigurationId;
        var before = await BlocksAsync(pageId);
        var toggle = before.Single(b => b.Type == BlockType.Toggle);
        Assert.Equal(2, before.Count(b => b.ParentBlockId == toggle.Id));

        var v1 = await SnapshotAsync(pageId);

        // Destroy the toggle and its subtree, then restore.
        await _client.DeleteAsync($"/api/blocks/{toggle.Id}");
        Assert.DoesNotContain(await BlocksAsync(pageId), b => b.Id == toggle.Id);

        var restore = await _client.PostAsync($"/api/pages/{pageId}/versions/{v1.VersionNumber}/restore", null);
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);

        var after = await BlocksAsync(pageId);
        Assert.Equal(before.Count, after.Count);
        var restoredToggle = after.Single(b => b.Type == BlockType.Toggle);
        // The restored children still point at the (id-preserved) toggle.
        Assert.Equal(2, after.Count(b => b.ParentBlockId == restoredToggle.Id));
    }
}
