using System.Net;
using System.Net.Http.Json;
using Folio.Api.Contracts;
using Folio.Domain.Enums;
using Folio.Infrastructure.Persistence;

namespace Folio.Tests;

/// <summary>Templates, deep-copy duplicate, and Markdown export.</summary>
public class TemplateTests : IDisposable
{
    private static readonly Guid WorkspaceId = DbSeeder.WorkspaceId;
    private readonly FolioApiFactory _factory = new();
    private readonly HttpClient _client;

    public TemplateTests() => _client = _factory.CreateAuthenticatedClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    // ---- templates ----

    [Fact]
    public async Task Create_template_from_page_then_instantiate_it()
    {
        var create = await _client.PostAsJsonAsync(
            $"/api/pages/{DbSeeder.GettingStartedId}/templates",
            new { name = "Onboarding", description = "Starter doc" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var template = await create.Content.ReadFromJsonAsync<TemplateResponse>(TestJson.Options);
        Assert.Equal("Onboarding", template!.Name);
        Assert.Equal(5, template.BlockCount); // seeded blocks on Getting Started

        var list = await _client.GetFromJsonAsync<List<TemplateResponse>>(
            $"/api/workspaces/{WorkspaceId}/templates", TestJson.Options);
        Assert.Contains(list!, t => t.Id == template.Id);

        var instantiate = await _client.PostAsJsonAsync(
            $"/api/workspaces/{WorkspaceId}/templates/{template.Id}/instantiate",
            new { parentId = (Guid?)null });
        Assert.Equal(HttpStatusCode.Created, instantiate.StatusCode);
        var newPage = await instantiate.Content.ReadFromJsonAsync<PageDetailResponse>(TestJson.Options);
        Assert.Equal("Getting Started", newPage!.Title);

        var newBlocks = await _client.GetFromJsonAsync<List<BlockResponse>>(
            $"/api/pages/{newPage.Id}/blocks", TestJson.Options);
        Assert.Equal(5, newBlocks!.Count);
    }

    [Fact]
    public async Task Viewer_cannot_instantiate_a_template()
    {
        var owner = _factory.CreateAuthenticatedClient(DbSeeder.OwnerEmail);
        var create = await owner.PostAsJsonAsync(
            $"/api/pages/{DbSeeder.GettingStartedId}/templates",
            new { name = "T" });
        var template = await create.Content.ReadFromJsonAsync<TemplateResponse>(TestJson.Options);

        var viewer = _factory.CreateAuthenticatedClient(DbSeeder.ViewerEmail);
        var instantiate = await viewer.PostAsJsonAsync(
            $"/api/workspaces/{WorkspaceId}/templates/{template!.Id}/instantiate",
            new { parentId = (Guid?)null });
        Assert.Equal(HttpStatusCode.Forbidden, instantiate.StatusCode);
    }

    // ---- duplicate ----

    [Fact]
    public async Task Duplicate_deep_copies_subtree_and_blocks()
    {
        var response = await _client.PostAsync($"/api/pages/{DbSeeder.GettingStartedId}/duplicate", null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var copy = await response.Content.ReadFromJsonAsync<PageDetailResponse>(TestJson.Options);
        Assert.Equal("Getting Started (copy)", copy!.Title);
        Assert.NotEqual(DbSeeder.GettingStartedId, copy.Id);

        // The copy has its own children with new ids.
        var tree = await _client.GetFromJsonAsync<List<PageTreeNode>>(
            $"/api/workspaces/{WorkspaceId}/pages/tree", TestJson.Options);
        var copyNode = tree!.First(n => n.Id == copy.Id);
        Assert.Equal(2, copyNode.Children.Count); // Installation + Configuration copies
        Assert.DoesNotContain(copyNode.Children, c => c.Id == DbSeeder.InstallationId);

        // Copied blocks exist independently.
        var copyBlocks = await _client.GetFromJsonAsync<List<BlockResponse>>(
            $"/api/pages/{copy.Id}/blocks", TestJson.Options);
        Assert.Equal(5, copyBlocks!.Count);
    }

    // ---- export ----

    [Fact]
    public async Task Export_page_renders_markdown()
    {
        var export = await _client.GetFromJsonAsync<ExportResponse>(
            $"/api/pages/{DbSeeder.GettingStartedId}/export", TestJson.Options);

        Assert.Equal("getting-started.md", export!.Filename);
        Assert.Contains("# 📘 Getting Started", export.Markdown);
        Assert.Contains("# Welcome to Folio", export.Markdown); // heading block, level 1
        Assert.Contains("- [x] Create your first page", export.Markdown); // checked todo
        Assert.Contains("> Docs are a love letter", export.Markdown); // quote
    }

    [Fact]
    public async Task Export_subtree_includes_child_pages()
    {
        var export = await _client.GetFromJsonAsync<ExportResponse>(
            $"/api/pages/{DbSeeder.GettingStartedId}/export?subtree=true", TestJson.Options);

        Assert.Contains("# 📘 Getting Started", export!.Markdown);
        Assert.Contains("## 📦 Installation", export.Markdown); // child page as a section
        Assert.Contains("```bash", export.Markdown); // code block from Installation
    }

    [Fact]
    public async Task Export_renders_v2_block_types()
    {
        var export = await _client.GetFromJsonAsync<ExportResponse>(
            $"/api/pages/{DbSeeder.ConfigurationId}/export", TestJson.Options);

        Assert.Contains("---", export!.Markdown); // divider
        Assert.Contains("![Configuration diagram]", export.Markdown); // image
        Assert.Contains("| Key | Value |", export.Markdown); // table header
    }
}
