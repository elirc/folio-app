using Folio.Api.Contracts;
using Folio.Domain.Entities;
using Folio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Folio.Api.Services;

/// <summary>Page tree operations: read, create, rename, move/reorder, delete.</summary>
public class PageService(FolioDbContext db)
{
    private static DateTime Now => DateTime.UtcNow;

    /// <summary>Returns the nested page tree for a workspace, or null if the workspace is unknown.</summary>
    public async Task<IReadOnlyList<PageTreeNode>?> GetTreeAsync(Guid workspaceId, CancellationToken ct)
    {
        if (!await db.Workspaces.AnyAsync(w => w.Id == workspaceId, ct))
        {
            return null;
        }

        var pages = await db.Pages
            .Where(p => p.WorkspaceId == workspaceId)
            .OrderBy(p => p.Position)
            .Select(p => new { p.Id, p.ParentId, p.Title, p.Icon, p.Position })
            .ToListAsync(ct);

        // ToLookup (unlike Dictionary) permits a null key, which is what root
        // pages have (ParentId == null); the indexer also returns an empty
        // sequence for parents with no children.
        var childrenByParent = pages
            .OrderBy(p => p.Position)
            .ToLookup(p => p.ParentId);

        IReadOnlyList<PageTreeNode> Build(Guid? parentId) =>
            childrenByParent[parentId]
                .Select(k => new PageTreeNode(k.Id, k.ParentId, k.Title, k.Icon, k.Position, Build(k.Id)))
                .ToList();

        return Build(null);
    }

    public async Task<PageDetailResponse?> GetDetailAsync(Guid pageId, CancellationToken ct)
    {
        var page = await db.Pages.FirstOrDefaultAsync(p => p.Id == pageId, ct);
        return page is null ? null : await ToDetailAsync(page, ct);
    }

    public async Task<IReadOnlyList<BreadcrumbItem>?> GetBreadcrumbAsync(Guid pageId, CancellationToken ct)
    {
        var page = await db.Pages
            .Where(p => p.Id == pageId)
            .Select(p => new { p.WorkspaceId })
            .FirstOrDefaultAsync(ct);

        return page is null ? null : await BuildBreadcrumbAsync(page.WorkspaceId, pageId, ct);
    }

    public async Task<ServiceResult<PageDetailResponse>> CreateAsync(Guid workspaceId, CreatePageRequest request, CancellationToken ct)
    {
        if (!await db.Workspaces.AnyAsync(w => w.Id == workspaceId, ct))
        {
            return ServiceResult<PageDetailResponse>.NotFound("Workspace not found.");
        }

        if (request.ParentId is Guid parentId &&
            !await db.Pages.AnyAsync(p => p.Id == parentId && p.WorkspaceId == workspaceId, ct))
        {
            return ServiceResult<PageDetailResponse>.Invalid("Parent page not found in this workspace.");
        }

        var siblings = await SiblingsAsync(workspaceId, request.ParentId, excluding: null, ct);
        var insertAt = Clamp(request.Position ?? siblings.Count, 0, siblings.Count);

        var page = new Page
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ParentId = request.ParentId,
            Title = request.Title!.Trim(),
            Icon = request.Icon,
            Position = insertAt,
            CreatedAt = Now,
            UpdatedAt = Now,
        };

        siblings.Insert(insertAt, page);
        Reindex(siblings);
        db.Pages.Add(page);
        await db.SaveChangesAsync(ct);

        return ServiceResult<PageDetailResponse>.Ok(await ToDetailAsync(page, ct));
    }

    public async Task<ServiceResult<PageDetailResponse>> UpdateAsync(Guid pageId, UpdatePageRequest request, CancellationToken ct)
    {
        var page = await db.Pages.FirstOrDefaultAsync(p => p.Id == pageId, ct);
        if (page is null)
        {
            return ServiceResult<PageDetailResponse>.NotFound("Page not found.");
        }

        page.Title = request.Title!.Trim();
        page.Icon = request.Icon;
        page.UpdatedAt = Now;
        await db.SaveChangesAsync(ct);

        return ServiceResult<PageDetailResponse>.Ok(await ToDetailAsync(page, ct));
    }

    public async Task<ServiceResult<PageDetailResponse>> MoveAsync(Guid pageId, MovePageRequest request, CancellationToken ct)
    {
        var page = await db.Pages.FirstOrDefaultAsync(p => p.Id == pageId, ct);
        if (page is null)
        {
            return ServiceResult<PageDetailResponse>.NotFound("Page not found.");
        }

        if (request.ParentId is Guid newParentId)
        {
            if (newParentId == pageId)
            {
                return ServiceResult<PageDetailResponse>.Invalid("A page cannot be its own parent.");
            }

            var newParent = await db.Pages.FirstOrDefaultAsync(p => p.Id == newParentId, ct);
            if (newParent is null || newParent.WorkspaceId != page.WorkspaceId)
            {
                return ServiceResult<PageDetailResponse>.Invalid("Target parent not found in this workspace.");
            }

            var descendants = await DescendantIdsAsync(page.WorkspaceId, pageId, ct);
            if (descendants.Contains(newParentId))
            {
                return ServiceResult<PageDetailResponse>.Invalid("Cannot move a page into its own descendant.");
            }
        }

        var oldParentId = page.ParentId;
        page.ParentId = request.ParentId;
        page.UpdatedAt = Now;

        var targetSiblings = await SiblingsAsync(page.WorkspaceId, request.ParentId, excluding: pageId, ct);
        var insertAt = Clamp(request.Position, 0, targetSiblings.Count);
        targetSiblings.Insert(insertAt, page);
        Reindex(targetSiblings);

        if (oldParentId != request.ParentId)
        {
            var oldSiblings = await SiblingsAsync(page.WorkspaceId, oldParentId, excluding: pageId, ct);
            Reindex(oldSiblings);
        }

        await db.SaveChangesAsync(ct);
        return ServiceResult<PageDetailResponse>.Ok(await ToDetailAsync(page, ct));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid pageId, CancellationToken ct)
    {
        var page = await db.Pages.FirstOrDefaultAsync(p => p.Id == pageId, ct);
        if (page is null)
        {
            return ServiceResult<bool>.NotFound("Page not found.");
        }

        var subtreeIds = await DescendantIdsAsync(page.WorkspaceId, pageId, ct);
        subtreeIds.Add(pageId);

        // Delete deepest-first so the self-referencing FK (Restrict) is never violated.
        var subtree = await db.Pages
            .Where(p => subtreeIds.Contains(p.Id))
            .ToListAsync(ct);
        var depth = DepthByPage(subtree);
        foreach (var p in subtree.OrderByDescending(p => depth[p.Id]))
        {
            db.Pages.Remove(p); // blocks cascade via FK
        }

        await db.SaveChangesAsync(ct);

        var remaining = await SiblingsAsync(page.WorkspaceId, page.ParentId, excluding: null, ct);
        Reindex(remaining);
        await db.SaveChangesAsync(ct);

        return ServiceResult<bool>.Ok(true);
    }

    // ---- helpers ----

    private async Task<List<Page>> SiblingsAsync(Guid workspaceId, Guid? parentId, Guid? excluding, CancellationToken ct)
    {
        var query = db.Pages.Where(p => p.WorkspaceId == workspaceId && p.ParentId == parentId);
        if (excluding is Guid id)
        {
            query = query.Where(p => p.Id != id);
        }

        return await query.OrderBy(p => p.Position).ToListAsync(ct);
    }

    private async Task<HashSet<Guid>> DescendantIdsAsync(Guid workspaceId, Guid rootId, CancellationToken ct)
    {
        var edges = await db.Pages
            .Where(p => p.WorkspaceId == workspaceId)
            .Select(p => new { p.Id, p.ParentId })
            .ToListAsync(ct);

        var childrenByParent = edges
            .Where(e => e.ParentId is not null)
            .GroupBy(e => e.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Id).ToList());

        var result = new HashSet<Guid>();
        var stack = new Stack<Guid>();
        stack.Push(rootId);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!childrenByParent.TryGetValue(current, out var kids))
            {
                continue;
            }

            foreach (var kid in kids)
            {
                if (result.Add(kid))
                {
                    stack.Push(kid);
                }
            }
        }

        return result;
    }

    private static Dictionary<Guid, int> DepthByPage(List<Page> pages)
    {
        var byId = pages.ToDictionary(p => p.Id);
        var depth = new Dictionary<Guid, int>();

        int DepthOf(Page p)
        {
            if (depth.TryGetValue(p.Id, out var d))
            {
                return d;
            }

            d = p.ParentId is Guid pid && byId.TryGetValue(pid, out var parent) ? DepthOf(parent) + 1 : 0;
            depth[p.Id] = d;
            return d;
        }

        foreach (var page in pages)
        {
            DepthOf(page);
        }

        return depth;
    }

    private static void Reindex(List<Page> ordered)
    {
        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].Position != i)
            {
                ordered[i].Position = i;
            }
        }
    }

    private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

    private async Task<PageDetailResponse> ToDetailAsync(Page page, CancellationToken ct)
    {
        var breadcrumb = await BuildBreadcrumbAsync(page.WorkspaceId, page.Id, ct);
        return new PageDetailResponse(
            page.Id,
            page.WorkspaceId,
            page.ParentId,
            page.Title,
            page.Icon,
            page.Position,
            page.CreatedAt,
            page.UpdatedAt,
            breadcrumb);
    }

    private async Task<IReadOnlyList<BreadcrumbItem>> BuildBreadcrumbAsync(Guid workspaceId, Guid pageId, CancellationToken ct)
    {
        var lookup = await db.Pages
            .Where(p => p.WorkspaceId == workspaceId)
            .Select(p => new { p.Id, p.ParentId, p.Title, p.Icon })
            .ToDictionaryAsync(p => p.Id, ct);

        var trail = new List<BreadcrumbItem>();
        Guid? cursor = pageId;
        var guard = 0;
        while (cursor is Guid id && lookup.TryGetValue(id, out var node) && guard++ < 1000)
        {
            trail.Add(new BreadcrumbItem(node.Id, node.Title, node.Icon));
            cursor = node.ParentId;
        }

        trail.Reverse();
        return trail;
    }
}
