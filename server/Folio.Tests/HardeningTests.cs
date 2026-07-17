using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Folio.Api.Contracts;
using Folio.Infrastructure.Persistence;

namespace Folio.Tests;

/// <summary>Cross-cutting concerns: ProblemDetails, validation, pagination. Read-only, shared factory.</summary>
public class HardeningTests : IClassFixture<FolioApiFactory>
{
    private static readonly Guid WorkspaceId = DbSeeder.WorkspaceId;
    private readonly FolioApiFactory _factory;

    public HardeningTests(FolioApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Not_found_returns_problem_details_json()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/pages/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(404, root.GetProperty("status").GetInt32());
        Assert.True(root.TryGetProperty("traceId", out _));
        Assert.True(root.TryGetProperty("instance", out _));
    }

    [Fact]
    public async Task Validation_error_returns_problem_with_field_errors()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/workspaces/{WorkspaceId}/pages",
            new { parentId = (Guid?)null }); // missing required Title

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("errors", out var errors));
        Assert.True(errors.EnumerateObject().Any());
    }

    [Fact]
    public async Task Recent_pages_returns_paged_metadata()
    {
        var client = _factory.CreateClient();

        var result = await client.GetFromJsonAsync<PagedResponse<PageListItemResponse>>(
            $"/api/workspaces/{WorkspaceId}/pages?page=1&pageSize=3");

        Assert.NotNull(result);
        Assert.Equal(3, result!.Items.Count);
        Assert.Equal(7, result.Total); // 7 seeded pages
        Assert.Equal(1, result.Page);
        Assert.Equal(3, result.PageSize);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task Recent_pages_includes_block_count_and_preview()
    {
        var client = _factory.CreateClient();

        var result = await client.GetFromJsonAsync<PagedResponse<PageListItemResponse>>(
            $"/api/workspaces/{WorkspaceId}/pages?page=1&pageSize=20");

        var gettingStarted = Assert.Single(result!.Items, i => i.Id == DbSeeder.GettingStartedId);
        Assert.Equal(5, gettingStarted.BlockCount);
        Assert.Contains("Welcome", gettingStarted.Preview);
    }

    [Fact]
    public async Task Recent_pages_rejects_invalid_pagination()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/workspaces/{WorkspaceId}/pages?page=1&pageSize=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
