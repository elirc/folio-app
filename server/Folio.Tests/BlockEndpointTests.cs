using System.Net;
using System.Net.Http.Json;
using Folio.Api.Contracts;
using Folio.Domain.Enums;
using Folio.Infrastructure.Persistence;

namespace Folio.Tests;

public class BlockEndpointTests : IDisposable
{
    private static readonly Guid PageId = DbSeeder.GettingStartedId;
    private readonly FolioApiFactory _factory = new();
    private readonly HttpClient _client;

    public BlockEndpointTests() => _client = _factory.CreateAuthenticatedClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<List<BlockResponse>> GetBlocksAsync() =>
        (await _client.GetFromJsonAsync<List<BlockResponse>>($"/api/pages/{PageId}/blocks", TestJson.Options))!;

    [Fact]
    public async Task List_returns_ordered_seeded_blocks()
    {
        var blocks = await GetBlocksAsync();

        Assert.Equal(5, blocks.Count);
        Assert.Equal(BlockType.Heading, blocks[0].Type);
        Assert.Equal([0, 1, 2, 3, 4], blocks.Select(b => b.Position));
        Assert.Equal("Welcome to Folio", blocks[0].Content.GetProperty("text").GetString());
    }

    [Fact]
    public async Task List_for_unknown_page_returns_404()
    {
        var response = await _client.GetAsync($"/api/pages/{Guid.NewGuid()}/blocks");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_appends_block_at_end()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/pages/{PageId}/blocks",
            new { type = "Paragraph", content = new { text = "A new paragraph." } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<BlockResponse>(TestJson.Options);
        Assert.Equal(BlockType.Paragraph, created!.Type);
        Assert.Equal(5, created.Position);
        Assert.Equal("A new paragraph.", created.Content.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Create_with_missing_type_returns_400()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/pages/{PageId}/blocks",
            new { content = new { text = "no type" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_with_non_object_content_returns_400()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/pages/{PageId}/blocks",
            new { type = "Paragraph", content = "just a string" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_changes_content_and_type()
    {
        var blocks = await GetBlocksAsync();
        var target = blocks[1]; // paragraph

        var response = await _client.PutAsJsonAsync(
            $"/api/blocks/{target.Id}",
            new { type = "Quote", content = new { text = "Now a quote." } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<BlockResponse>(TestJson.Options);
        Assert.Equal(BlockType.Quote, updated!.Type);
        Assert.Equal("Now a quote.", updated.Content.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Move_reorders_blocks()
    {
        var blocks = await GetBlocksAsync();
        var last = blocks[^1];

        var response = await _client.PostAsJsonAsync($"/api/blocks/{last.Id}/move", new { position = 0 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var reordered = await GetBlocksAsync();
        Assert.Equal(last.Id, reordered[0].Id);
        Assert.Equal([0, 1, 2, 3, 4], reordered.Select(b => b.Position));
    }

    [Fact]
    public async Task Delete_removes_and_reindexes()
    {
        var blocks = await GetBlocksAsync();
        var first = blocks[0];

        var response = await _client.DeleteAsync($"/api/blocks/{first.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var remaining = await GetBlocksAsync();
        Assert.Equal(4, remaining.Count);
        Assert.DoesNotContain(remaining, b => b.Id == first.Id);
        Assert.Equal([0, 1, 2, 3], remaining.Select(b => b.Position));
    }

    // ---- v2 block types + nesting ----

    private async Task<List<BlockResponse>> GetBlocksForAsync(Guid pageId) =>
        (await _client.GetFromJsonAsync<List<BlockResponse>>($"/api/pages/{pageId}/blocks", TestJson.Options))!;

    [Fact]
    public async Task Seeded_configuration_page_has_v2_types_and_nested_children()
    {
        var blocks = await GetBlocksForAsync(DbSeeder.ConfigurationId);

        // Callout, Toggle + 2 children, Divider, Image, Table = 7 blocks total.
        Assert.Equal(7, blocks.Count);
        Assert.Contains(blocks, b => b.Type == BlockType.Callout);
        Assert.Contains(blocks, b => b.Type == BlockType.Divider);
        Assert.Contains(blocks, b => b.Type == BlockType.Image);
        Assert.Contains(blocks, b => b.Type == BlockType.Table);

        var toggle = Assert.Single(blocks, b => b.Type == BlockType.Toggle);
        var children = blocks.Where(b => b.ParentBlockId == toggle.Id).ToList();
        Assert.Equal(2, children.Count);
        // Pre-order DFS: the toggle appears immediately before its children.
        var toggleIndex = blocks.FindIndex(b => b.Id == toggle.Id);
        Assert.Equal(toggle.Id, blocks[toggleIndex + 1].ParentBlockId);
    }

    [Fact]
    public async Task Create_child_under_toggle_nests_it()
    {
        var toggle = await CreateToggleAsync();

        var response = await _client.PostAsJsonAsync(
            $"/api/pages/{PageId}/blocks",
            new { type = "Paragraph", content = new { text = "inside toggle" }, parentId = toggle.Id });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var child = await response.Content.ReadFromJsonAsync<BlockResponse>(TestJson.Options);
        Assert.Equal(toggle.Id, child!.ParentBlockId);
        Assert.Equal(0, child.Position);
    }

    [Fact]
    public async Task Create_child_under_non_toggle_is_rejected()
    {
        var blocks = await GetBlocksAsync();
        var paragraph = blocks.First(b => b.Type == BlockType.Paragraph);

        var response = await _client.PostAsJsonAsync(
            $"/api/pages/{PageId}/blocks",
            new { type = "Paragraph", content = new { text = "nope" }, parentId = paragraph.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Move_block_between_parents_reindexes_both_groups()
    {
        var toggle = await CreateToggleAsync();

        // A root paragraph moved under the toggle becomes its child at position 0.
        var blocks = await GetBlocksAsync();
        var paragraph = blocks.First(b => b.Type == BlockType.Paragraph);

        var move = await _client.PostAsJsonAsync(
            $"/api/blocks/{paragraph.Id}/move",
            new { position = 0, parentId = toggle.Id });
        Assert.Equal(HttpStatusCode.OK, move.StatusCode);

        var moved = await move.Content.ReadFromJsonAsync<BlockResponse>(TestJson.Options);
        Assert.Equal(toggle.Id, moved!.ParentBlockId);

        var all = await GetBlocksAsync();
        var roots = all.Where(b => b.ParentBlockId == null).OrderBy(b => b.Position).ToList();
        Assert.Equal(Enumerable.Range(0, roots.Count), roots.Select(b => b.Position));
    }

    [Fact]
    public async Task Move_toggle_into_its_own_descendant_is_rejected()
    {
        var toggle = await CreateToggleAsync();
        var childResponse = await _client.PostAsJsonAsync(
            $"/api/pages/{PageId}/blocks",
            new { type = "Toggle", content = new { text = "child toggle", collapsed = false }, parentId = toggle.Id });
        var child = await childResponse.Content.ReadFromJsonAsync<BlockResponse>(TestJson.Options);

        var move = await _client.PostAsJsonAsync(
            $"/api/blocks/{toggle.Id}/move",
            new { position = 0, parentId = child!.Id });

        Assert.Equal(HttpStatusCode.BadRequest, move.StatusCode);
    }

    [Fact]
    public async Task Delete_toggle_removes_its_child_subtree()
    {
        var toggle = await CreateToggleAsync();
        await _client.PostAsJsonAsync(
            $"/api/pages/{PageId}/blocks",
            new { type = "Paragraph", content = new { text = "child" }, parentId = toggle.Id });

        var before = await GetBlocksAsync();
        Assert.Contains(before, b => b.ParentBlockId == toggle.Id);

        await _client.DeleteAsync($"/api/blocks/{toggle.Id}");

        var after = await GetBlocksAsync();
        Assert.DoesNotContain(after, b => b.Id == toggle.Id);
        Assert.DoesNotContain(after, b => b.ParentBlockId == toggle.Id);
    }

    private async Task<BlockResponse> CreateToggleAsync()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/pages/{PageId}/blocks",
            new { type = "Toggle", content = new { text = "Toggle", collapsed = false } });
        return (await response.Content.ReadFromJsonAsync<BlockResponse>(TestJson.Options))!;
    }
}
