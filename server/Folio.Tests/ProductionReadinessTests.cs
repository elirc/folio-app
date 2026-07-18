using System.Net;
using System.Net.Http.Json;
using Folio.Api.Contracts;
using Folio.Infrastructure.Persistence;

namespace Folio.Tests;

/// <summary>Sprint 15 hardening: optimistic concurrency (409) and write rate limiting (429).</summary>
public class ProductionReadinessTests
{
    private static readonly Guid WorkspaceId = DbSeeder.WorkspaceId;

    // ---- optimistic concurrency ----

    [Fact]
    public async Task Page_update_with_stale_version_returns_409()
    {
        using var factory = new FolioApiFactory();
        var client = factory.CreateAuthenticatedClient();

        var page = await client.GetFromJsonAsync<PageDetailResponse>(
            $"/api/pages/{DbSeeder.ProductId}", TestJson.Options);
        var staleVersion = page!.Version;

        // First write succeeds and rotates the version.
        var first = await client.PutAsJsonAsync(
            $"/api/pages/{DbSeeder.ProductId}",
            new { title = "Roadmap v2", icon = (string?)null, expectedVersion = staleVersion });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // A second write with the now-stale version is rejected.
        var second = await client.PutAsJsonAsync(
            $"/api/pages/{DbSeeder.ProductId}",
            new { title = "Roadmap v3", icon = (string?)null, expectedVersion = staleVersion });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Block_update_with_stale_version_returns_409()
    {
        using var factory = new FolioApiFactory();
        var client = factory.CreateAuthenticatedClient();

        var blocks = await client.GetFromJsonAsync<List<BlockResponse>>(
            $"/api/pages/{DbSeeder.GettingStartedId}/blocks", TestJson.Options);
        var target = blocks![1];
        var staleVersion = target.Version;

        var first = await client.PutAsJsonAsync(
            $"/api/blocks/{target.Id}",
            new { type = "Paragraph", content = new { text = "edit one" }, expectedVersion = staleVersion });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PutAsJsonAsync(
            $"/api/blocks/{target.Id}",
            new { type = "Paragraph", content = new { text = "edit two" }, expectedVersion = staleVersion });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Update_without_expected_version_skips_the_check()
    {
        using var factory = new FolioApiFactory();
        var client = factory.CreateAuthenticatedClient();

        // No expectedVersion → backward-compatible, always applies.
        var response = await client.PutAsJsonAsync(
            $"/api/pages/{DbSeeder.ProductId}",
            new { title = "No version check", icon = (string?)null });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ---- rate limiting ----

    [Fact]
    public async Task Write_endpoints_are_rate_limited_with_429()
    {
        // A tiny per-user write budget so a burst trips the limiter.
        using var factory = new FolioApiFactory { WritePermitLimit = 3 };
        var client = factory.CreateAuthenticatedClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            var response = await client.PostAsJsonAsync(
                $"/api/workspaces/{WorkspaceId}/pages",
                new { title = $"Burst {i}" });
            statuses.Add(response.StatusCode);
        }

        Assert.Equal(3, statuses.Count(s => s == HttpStatusCode.Created));
        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }

    [Fact]
    public async Task Reads_are_not_rate_limited()
    {
        using var factory = new FolioApiFactory { WritePermitLimit = 1 };
        var client = factory.CreateAuthenticatedClient();

        // Many GETs stay under the exemption regardless of the tiny write budget.
        for (var i = 0; i < 5; i++)
        {
            var response = await client.GetAsync($"/api/workspaces/{WorkspaceId}/pages/tree");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
