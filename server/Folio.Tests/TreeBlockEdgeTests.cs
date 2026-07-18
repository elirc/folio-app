using System.Net;
using System.Net.Http.Json;
using Folio.Api.Contracts;
using Folio.Domain.Enums;
using Folio.Infrastructure.Persistence;

namespace Folio.Tests;

/// <summary>
/// Tree + block structural edge cases: self/descendant move cycles, position
/// clamping at the boundaries, deep toggle nesting, orphan handling on
/// delete/restore, and moving blocks between parents.
/// </summary>
public class TreeBlockEdgeTests : IDisposable
{
    private static readonly Guid WorkspaceId = DbSeeder.WorkspaceId;
    private static readonly Guid PageId = DbSeeder.GettingStartedId;
    private readonly FolioApiFactory _factory = new();
    private readonly HttpClient _client;

    public TreeBlockEdgeTests() => _client = _factory.CreateAuthenticatedClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private Task<List<PageTreeNode>> TreeAsync() =>
        _client.GetFromJsonAsync<List<PageTreeNode>>($"/api/workspaces/{WorkspaceId}/pages/tree", TestJson.Options)!;

    private Task<List<BlockResponse>> BlocksAsync(Guid pageId) =>
        _client.GetFromJsonAsync<List<BlockResponse>>($"/api/pages/{pageId}/blocks", TestJson.Options)!;

    // ---- page move cycles ----

    [Fact]
    public async Task Page_cannot_be_moved_under_itself()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/pages/{DbSeeder.GettingStartedId}/move",
            new { parentId = DbSeeder.GettingStartedId, position = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Page_cannot_be_moved_under_a_deep_descendant()
    {
        // Installation is a child of Getting Started; nest a grandchild under it.
        var grandchild = await _client.PostAsJsonAsync(
            $"/api/workspaces/{WorkspaceId}/pages",
            new { title = "Prereqs", parentId = DbSeeder.InstallationId });
        var g = await grandchild.Content.ReadFromJsonAsync<PageDetailResponse>(TestJson.Options);

        // Moving Getting Started under its own grandchild is a cycle → rejected.
        var move = await _client.PostAsJsonAsync(
            $"/api/pages/{DbSeeder.GettingStartedId}/move",
            new { parentId = g!.Id, position = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, move.StatusCode);
    }

    [Fact]
    public async Task Page_move_position_is_clamped_to_the_sibling_count()
    {
        // Move Engineering to a wildly out-of-range position; it clamps to the end.
        var move = await _client.PostAsJsonAsync(
            $"/api/pages/{DbSeeder.EngineeringId}/move",
            new { parentId = (Guid?)null, position = 999 });
        Assert.Equal(HttpStatusCode.OK, move.StatusCode);

        var tree = await TreeAsync();
        var roots = tree.Select(n => n.Position).ToList();
        // Positions stay a contiguous 0..n-1 sequence with no gaps.
        Assert.Equal(Enumerable.Range(0, tree.Count), roots);
        Assert.Equal("Engineering", tree[^1].Title);
    }

    // ---- deep toggle nesting ----

    [Fact]
    public async Task Toggles_nest_multiple_levels_deep_in_preorder()
    {
        var outer = await CreateBlockAsync(new { type = "Toggle", content = new { text = "Outer", collapsed = false } });
        var inner = await CreateBlockAsync(new { type = "Toggle", content = new { text = "Inner", collapsed = false }, parentId = outer.Id });
        var leaf = await CreateBlockAsync(new { type = "Paragraph", content = new { text = "Leaf" }, parentId = inner.Id });

        var blocks = await BlocksAsync(PageId);
        var idx = blocks.Select(b => b.Id).ToList();

        // Pre-order DFS: outer immediately precedes inner, which precedes leaf.
        Assert.True(idx.IndexOf(outer.Id) < idx.IndexOf(inner.Id));
        Assert.True(idx.IndexOf(inner.Id) < idx.IndexOf(leaf.Id));
        Assert.Equal(outer.Id, blocks.First(b => b.Id == inner.Id).ParentBlockId);
        Assert.Equal(inner.Id, blocks.First(b => b.Id == leaf.Id).ParentBlockId);
    }

    [Fact]
    public async Task Block_cannot_be_moved_under_its_own_descendant()
    {
        var outer = await CreateBlockAsync(new { type = "Toggle", content = new { text = "Outer", collapsed = false } });
        var inner = await CreateBlockAsync(new { type = "Toggle", content = new { text = "Inner", collapsed = false }, parentId = outer.Id });

        var move = await _client.PostAsJsonAsync(
            $"/api/blocks/{outer.Id}/move",
            new { position = 0, parentId = inner.Id });

        Assert.Equal(HttpStatusCode.BadRequest, move.StatusCode);
    }

    [Fact]
    public async Task Block_moved_out_of_a_toggle_reindexes_both_groups()
    {
        var toggle = await CreateBlockAsync(new { type = "Toggle", content = new { text = "T", collapsed = false } });
        var childA = await CreateBlockAsync(new { type = "Bulleted", content = new { text = "A" }, parentId = toggle.Id });
        await CreateBlockAsync(new { type = "Bulleted", content = new { text = "B" }, parentId = toggle.Id });

        // Move childA out to the page root at position 0.
        var move = await _client.PostAsJsonAsync(
            $"/api/blocks/{childA.Id}/move",
            new { position = 0, parentId = (Guid?)null });
        Assert.Equal(HttpStatusCode.OK, move.StatusCode);
        var moved = await move.Content.ReadFromJsonAsync<BlockResponse>(TestJson.Options);
        Assert.Null(moved!.ParentBlockId);

        var blocks = await BlocksAsync(PageId);
        var roots = blocks.Where(b => b.ParentBlockId == null).OrderBy(b => b.Position).ToList();
        var toggleChildren = blocks.Where(b => b.ParentBlockId == toggle.Id).OrderBy(b => b.Position).ToList();
        // Both sibling groups keep contiguous positions after the cross-parent move.
        Assert.Equal(Enumerable.Range(0, roots.Count), roots.Select(b => b.Position));
        Assert.Equal(Enumerable.Range(0, toggleChildren.Count), toggleChildren.Select(b => b.Position));
        Assert.Single(toggleChildren); // only "B" remains under the toggle
    }

    // ---- orphan handling on delete / restore ----

    [Fact]
    public async Task Restoring_a_child_whose_parent_is_still_trashed_reparents_to_root()
    {
        // Trash the whole Getting Started subtree.
        await _client.DeleteAsync($"/api/pages/{DbSeeder.GettingStartedId}");

        // Restore only the child Installation; its parent is still in the trash.
        var restore = await _client.PostAsync($"/api/pages/{DbSeeder.InstallationId}/restore", null);
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
        var restored = await restore.Content.ReadFromJsonAsync<PageDetailResponse>(TestJson.Options);

        // It never dangles under a deleted ancestor — it comes back as a root.
        Assert.Null(restored!.ParentId);

        var tree = await TreeAsync();
        Assert.Contains(tree, n => n.Id == DbSeeder.InstallationId && n.ParentId == null);
        // Getting Started is still trashed and absent from the tree.
        Assert.DoesNotContain(tree, n => n.Id == DbSeeder.GettingStartedId);
    }

    [Fact]
    public async Task Deleting_a_page_reindexes_the_remaining_root_siblings()
    {
        // Delete the middle root (Engineering) and confirm no positional gap remains.
        await _client.DeleteAsync($"/api/pages/{DbSeeder.EngineeringId}");

        var tree = await TreeAsync();
        Assert.Equal(["Getting Started", "Product"], tree.Select(n => n.Title));
        Assert.Equal(Enumerable.Range(0, tree.Count), tree.Select(n => n.Position));
    }

    private async Task<BlockResponse> CreateBlockAsync(object body)
    {
        var response = await _client.PostAsJsonAsync($"/api/pages/{PageId}/blocks", body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BlockResponse>(TestJson.Options))!;
    }
}
