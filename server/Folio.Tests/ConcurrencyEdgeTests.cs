using System.Net;
using System.Net.Http.Json;
using Folio.Api.Contracts;
using Folio.Infrastructure.Persistence;

namespace Folio.Tests;

/// <summary>
/// Concurrency + throttling edge cases: interleaved block reorders keep positions
/// contiguous, a stale block write still 409s after a reorder, and the write rate
/// limiter recovers once its window elapses.
/// </summary>
public class ConcurrencyEdgeTests
{
    private static readonly Guid WorkspaceId = DbSeeder.WorkspaceId;
    private static readonly Guid PageId = DbSeeder.GettingStartedId;

    private static Task<List<BlockResponse>> BlocksAsync(HttpClient client) =>
        client.GetFromJsonAsync<List<BlockResponse>>($"/api/pages/{PageId}/blocks", TestJson.Options)!;

    [Fact]
    public async Task Interleaved_reorders_keep_positions_contiguous()
    {
        using var factory = new FolioApiFactory();
        var client = factory.CreateAuthenticatedClient();

        var original = await BlocksAsync(client);
        var ids = original.Select(b => b.Id).ToHashSet();

        // A burst of reorders bouncing blocks to the ends of the list.
        await client.PostAsJsonAsync($"/api/blocks/{original[0].Id}/move", new { position = 99 });
        await client.PostAsJsonAsync($"/api/blocks/{original[4].Id}/move", new { position = 0 });
        await client.PostAsJsonAsync($"/api/blocks/{original[2].Id}/move", new { position = 1 });
        await client.PostAsJsonAsync($"/api/blocks/{original[1].Id}/move", new { position = 99 });

        var after = await BlocksAsync(client);
        // No blocks lost or duplicated, and positions remain a clean 0..n-1 run.
        Assert.Equal(ids, after.Select(b => b.Id).ToHashSet());
        Assert.Equal(Enumerable.Range(0, after.Count), after.OrderBy(b => b.Position).Select(b => b.Position));
    }

    [Fact]
    public async Task Stale_block_write_after_a_reorder_still_conflicts()
    {
        using var factory = new FolioApiFactory();
        var client = factory.CreateAuthenticatedClient();

        var blocks = await BlocksAsync(client);
        var target = blocks[1];
        var staleVersion = target.Version;

        // A successful content edit rotates the version.
        var first = await client.PutAsJsonAsync(
            $"/api/blocks/{target.Id}",
            new { type = "Paragraph", content = new { text = "fresh edit" }, expectedVersion = staleVersion });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // A reorder (which carries no version) must not mask the stale write: a
        // second edit against the old version still conflicts.
        await client.PostAsJsonAsync($"/api/blocks/{target.Id}/move", new { position = 0 });
        var stale = await client.PutAsJsonAsync(
            $"/api/blocks/{target.Id}",
            new { type = "Paragraph", content = new { text = "stale edit" }, expectedVersion = staleVersion });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task Write_rate_limiter_recovers_after_its_window_elapses()
    {
        // A small budget over a short window: a burst trips it, and a later write
        // (once the window rolls over) is admitted again. Assertions avoid exact
        // timing so the test is stable under parallel-suite CPU contention.
        using var factory = new FolioApiFactory { WritePermitLimit = 2, WriteWindowSeconds = 3 };
        var client = factory.CreateAuthenticatedClient();

        async Task<HttpStatusCode> CreateAsync(string title) =>
            (await client.PostAsJsonAsync($"/api/workspaces/{WorkspaceId}/pages", new { title })).StatusCode;

        // A burst past the budget: at least one write is admitted and at least one
        // is throttled (the exact split depends on scheduling, so we don't pin it).
        var burst = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            burst.Add(await CreateAsync($"Burst {i}"));
        }
        Assert.Contains(HttpStatusCode.Created, burst);
        Assert.Contains(HttpStatusCode.TooManyRequests, burst);

        // Once the window rolls over the limiter admits writes again. Poll so the
        // replenishment-timer jitter under load can't flake the assertion.
        var recovered = HttpStatusCode.TooManyRequests;
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(400);
            recovered = await CreateAsync("After window");
            if (recovered == HttpStatusCode.Created)
            {
                break;
            }
        }
        Assert.Equal(HttpStatusCode.Created, recovered);
    }
}
