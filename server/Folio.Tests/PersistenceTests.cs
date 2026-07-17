using System.Net;
using System.Net.Http.Json;
using Folio.Api.Contracts;
using Folio.Domain.Enums;
using Folio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Folio.Tests;

public class PersistenceTests : IClassFixture<FolioApiFactory>
{
    private readonly FolioApiFactory _factory;

    public PersistenceTests(FolioApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Seed_creates_workspace_with_members_and_page_tree()
    {
        var counts = await _factory.WithDbAsync(async db => new
        {
            Workspaces = await db.Workspaces.CountAsync(),
            Members = await db.Members.CountAsync(),
            Pages = await db.Pages.CountAsync(),
            Blocks = await db.Blocks.CountAsync(),
        });

        // Two workspaces: "Acme Docs" (3 members, 7 pages) + isolated "Globex" (1 member, 1 page).
        Assert.Equal(2, counts.Workspaces);
        Assert.Equal(4, counts.Members);
        Assert.Equal(8, counts.Pages);
        Assert.True(counts.Blocks > 0);
    }

    [Fact]
    public async Task Getting_started_page_has_two_ordered_children()
    {
        var children = await _factory.WithDbAsync(async db =>
            await db.Pages
                .Where(p => p.ParentId == DbSeeder.GettingStartedId)
                .OrderBy(p => p.Position)
                .Select(p => p.Title)
                .ToListAsync());

        Assert.Equal(["Installation", "Configuration"], children);
    }

    [Fact]
    public async Task Block_type_round_trips_as_string_enum()
    {
        var (type, position) = await _factory.WithDbAsync(async db =>
        {
            var first = await db.Blocks
                .Where(b => b.PageId == DbSeeder.GettingStartedId)
                .OrderBy(b => b.Position)
                .FirstAsync();
            return (first.Type, first.Position);
        });

        Assert.Equal(BlockType.Heading, type);
        Assert.Equal(0, position);
    }

    [Fact]
    public async Task Workspaces_endpoint_returns_callers_workspace_with_counts()
    {
        // Authenticated as the Acme owner — the endpoint returns only their workspace.
        var client = _factory.CreateAuthenticatedClient();

        var response = await client.GetAsync("/api/workspaces");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var workspaces = await response.Content.ReadFromJsonAsync<List<WorkspaceSummaryResponse>>();
        Assert.NotNull(workspaces);
        var acme = Assert.Single(workspaces!);
        Assert.Equal("Acme Docs", acme.Name);
        Assert.Equal(3, acme.MemberCount);
        Assert.Equal(7, acme.PageCount);
    }
}
