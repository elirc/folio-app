using Folio.Api.Contracts;
using Folio.Domain.Entities;
using Folio.Domain.Enums;
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
            .Select(p => new { p.Id, p.ParentId, p.Title, p.Icon, p.Position, p.IsFavorite })
            .ToListAsync(ct);

        // ToLookup (unlike Dictionary) permits a null key, which is what root
        // pages have (ParentId == null); the indexer also returns an empty
        // sequence for parents with no children.
        var childrenByParent = pages
            .OrderBy(p => p.Position)
            .ToLookup(p => p.ParentId);

        IReadOnlyList<PageTreeNode> Build(Guid? parentId) =>
            childrenByParent[parentId]
                .Select(k => new PageTreeNode(k.Id, k.ParentId, k.Title, k.Icon, k.Position, k.IsFavorite, Build(k.Id)))
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

    /// <summary>Soft-deletes a page and its subtree (moves them to the trash).</summary>
    public async Task<ServiceResult<bool>> DeleteAsync(Guid pageId, CancellationToken ct)
    {
        var page = await db.Pages.FirstOrDefaultAsync(p => p.Id == pageId, ct);
        if (page is null)
        {
            return ServiceResult<bool>.NotFound("Page not found.");
        }

        var subtreeIds = await DescendantIdsAsync(page.WorkspaceId, pageId, ct);
        subtreeIds.Add(pageId);

        var subtree = await db.Pages
            .Where(p => subtreeIds.Contains(p.Id))
            .ToListAsync(ct);
        var when = Now;
        foreach (var p in subtree)
        {
            p.IsDeleted = true;
            p.DeletedAt = when;
        }

        await db.SaveChangesAsync(ct);

        // Remaining siblings (query filter already hides the just-trashed page).
        var remaining = await SiblingsAsync(page.WorkspaceId, page.ParentId, excluding: null, ct);
        Reindex(remaining);
        await db.SaveChangesAsync(ct);

        return ServiceResult<bool>.Ok(true);
    }

    /// <summary>Restores a trashed page (and its trashed subtree) to the tree.</summary>
    public async Task<ServiceResult<PageDetailResponse>> RestoreAsync(Guid pageId, CancellationToken ct)
    {
        var page = await db.Pages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == pageId && p.IsDeleted, ct);
        if (page is null)
        {
            return ServiceResult<PageDetailResponse>.NotFound("Trashed page not found.");
        }

        // If the parent is missing or still trashed, restore to the root so the
        // page never dangles under a deleted ancestor.
        var parentAlive = page.ParentId is Guid parentId
            && await db.Pages.AnyAsync(p => p.Id == parentId, ct);
        if (!parentAlive)
        {
            page.ParentId = null;
        }

        var subtreeIds = await DeletedDescendantIdsAsync(page.WorkspaceId, pageId, ct);
        subtreeIds.Add(pageId);
        var subtree = await db.Pages
            .IgnoreQueryFilters()
            .Where(p => subtreeIds.Contains(p.Id))
            .ToListAsync(ct);
        foreach (var p in subtree)
        {
            p.IsDeleted = false;
            p.DeletedAt = null;
        }

        // Append the restored root at the end of its (now live) sibling list.
        var siblings = await SiblingsAsync(page.WorkspaceId, page.ParentId, excluding: pageId, ct);
        page.Position = siblings.Count;
        page.UpdatedAt = Now;

        await db.SaveChangesAsync(ct);
        return ServiceResult<PageDetailResponse>.Ok(await ToDetailAsync(page, ct));
    }

    /// <summary>Lists the roots of trashed subtrees for a workspace.</summary>
    public async Task<IReadOnlyList<TrashItemResponse>?> GetTrashAsync(Guid workspaceId, CancellationToken ct)
    {
        if (!await db.Workspaces.AnyAsync(w => w.Id == workspaceId, ct))
        {
            return null;
        }

        var deleted = await db.Pages
            .IgnoreQueryFilters()
            .Where(p => p.WorkspaceId == workspaceId && p.IsDeleted)
            .Select(p => new { p.Id, p.ParentId, p.Title, p.Icon, p.DeletedAt })
            .ToListAsync(ct);

        var deletedIds = deleted.Select(p => p.Id).ToHashSet();

        // Only surface the top of each trashed subtree (its parent isn't itself trashed).
        return deleted
            .Where(p => p.ParentId is null || !deletedIds.Contains(p.ParentId.Value))
            .OrderByDescending(p => p.DeletedAt)
            .Select(p => new TrashItemResponse(p.Id, p.Title, p.Icon, p.DeletedAt))
            .ToList();
    }

    public async Task<ServiceResult<ShareResponse>> SetShareAsync(Guid pageId, ShareRequest request, CancellationToken ct)
    {
        var page = await db.Pages.FirstOrDefaultAsync(p => p.Id == pageId, ct);
        if (page is null)
        {
            return ServiceResult<ShareResponse>.NotFound("Page not found.");
        }

        page.Visibility = request.Visibility!.Value;
        page.Permission = request.Permission!.Value;

        // Public pages get a stable slug; non-public pages drop theirs.
        if (page.Visibility == PageVisibility.Public)
        {
            page.PublicSlug ??= NewSlug();
        }
        else
        {
            page.PublicSlug = null;
        }

        page.UpdatedAt = Now;
        await db.SaveChangesAsync(ct);

        return ServiceResult<ShareResponse>.Ok(new ShareResponse(page.Visibility, page.Permission, page.PublicSlug));
    }

    public async Task<ServiceResult<PageDetailResponse>> SetFavoriteAsync(Guid pageId, bool isFavorite, CancellationToken ct)
    {
        var page = await db.Pages.FirstOrDefaultAsync(p => p.Id == pageId, ct);
        if (page is null)
        {
            return ServiceResult<PageDetailResponse>.NotFound("Page not found.");
        }

        page.IsFavorite = isFavorite;
        page.UpdatedAt = Now;
        await db.SaveChangesAsync(ct);

        return ServiceResult<PageDetailResponse>.Ok(await ToDetailAsync(page, ct));
    }

    public async Task<IReadOnlyList<FavoriteResponse>?> GetFavoritesAsync(Guid workspaceId, CancellationToken ct)
    {
        if (!await db.Workspaces.AnyAsync(w => w.Id == workspaceId, ct))
        {
            return null;
        }

        return await db.Pages
            .Where(p => p.WorkspaceId == workspaceId && p.IsFavorite)
            .OrderBy(p => p.Title)
            .Select(p => new FavoriteResponse(p.Id, p.Title, p.Icon))
            .ToListAsync(ct);
    }

    /// <summary>A paginated, most-recently-updated-first list of a workspace's pages.</summary>
    public async Task<PagedResponse<PageListItemResponse>?> GetRecentAsync(
        Guid workspaceId,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        if (!await db.Workspaces.AnyAsync(w => w.Id == workspaceId, ct))
        {
            return null;
        }

        var baseQuery = db.Pages.Where(p => p.WorkspaceId == workspaceId);
        var total = await baseQuery.CountAsync(ct);

        var rows = await baseQuery
            .OrderByDescending(p => p.UpdatedAt)
            .ThenBy(p => p.Title)
            // Include must come before Skip/Take — placing it after paginates
            // the joined rows instead of the pages.
            .Include(p => p.Blocks)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = rows
            .Select(p => new PageListItemResponse(
                p.Id,
                p.Title,
                p.Icon,
                p.Blocks.Count,
                Preview(p.Blocks),
                p.UpdatedAt))
            .ToList();

        var totalPages = pageSize == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
        return new PagedResponse<PageListItemResponse>(items, page, pageSize, total, totalPages);
    }

    private static string? Preview(ICollection<Block> blocks)
    {
        var first = blocks.OrderBy(b => b.Position).FirstOrDefault();
        if (first is null)
        {
            return null;
        }

        try
        {
            var element = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(first.Content);
            if (element.ValueKind == System.Text.Json.JsonValueKind.Object
                && element.TryGetProperty("text", out var text))
            {
                var value = text.GetString();
                return value is { Length: > 120 } ? value[..120].TrimEnd() + "…" : value;
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // fall through
        }

        return null;
    }

    /// <summary>Resolves a page by its public slug (only if still public).</summary>
    public async Task<PageDetailResponse?> GetPublicBySlugAsync(string slug, CancellationToken ct)
    {
        var page = await db.Pages
            .FirstOrDefaultAsync(p => p.PublicSlug == slug && p.Visibility == PageVisibility.Public, ct);
        return page is null ? null : await ToDetailAsync(page, ct);
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

    /// <summary>Descendants of <paramref name="rootId"/> that are currently soft-deleted.</summary>
    private async Task<HashSet<Guid>> DeletedDescendantIdsAsync(Guid workspaceId, Guid rootId, CancellationToken ct)
    {
        var nodes = await db.Pages
            .IgnoreQueryFilters()
            .Where(p => p.WorkspaceId == workspaceId)
            .Select(p => new { p.Id, p.ParentId, p.IsDeleted })
            .ToListAsync(ct);

        var childrenByParent = nodes
            .Where(n => n.ParentId is not null)
            .GroupBy(n => n.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

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

            foreach (var kid in kids.Where(k => k.IsDeleted))
            {
                if (result.Add(kid.Id))
                {
                    stack.Push(kid.Id);
                }
            }
        }

        return result;
    }

    private static string NewSlug() => Guid.NewGuid().ToString("N")[..12];

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
            page.Visibility,
            page.Permission,
            page.PublicSlug,
            page.IsFavorite,
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
