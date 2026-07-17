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

    public BlockEndpointTests() => _client = _factory.CreateClient();

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
}
