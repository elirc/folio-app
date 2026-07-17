using System.Text.Json;
using Folio.Api.Auth;
using Folio.Api.Contracts;
using Folio.Domain.Entities;
using Folio.Domain.Enums;
using Folio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Folio.Api.Services;

/// <summary>Typed content-block operations, ordered per page.</summary>
public class BlockService(FolioDbContext db, ICurrentMemberAccessor current)
{
    private static DateTime Now => DateTime.UtcNow;

    private CurrentMember? Member => current.Member;

    /// <summary>Blocks for a page in order (403/404 when the page isn't readable).</summary>
    public async Task<ServiceResult<IReadOnlyList<BlockResponse>>> GetForPageAsync(Guid pageId, CancellationToken ct)
    {
        var info = await PageAuthInfoAsync(pageId, ct);
        if (info is null)
        {
            return ServiceResult<IReadOnlyList<BlockResponse>>.NotFound("Page not found.");
        }

        if (Guard<IReadOnlyList<BlockResponse>>(PageAuthorization.CanRead(Member, info.WorkspaceId, info.Visibility)) is { } denied)
        {
            return denied;
        }

        var blocks = await db.Blocks
            .Where(b => b.PageId == pageId)
            .ToListAsync(ct);

        // Pre-order DFS: each parent immediately precedes its children, siblings
        // in Position order — a flat list the client re-nests via ParentBlockId.
        return ServiceResult<IReadOnlyList<BlockResponse>>.Ok(OrderTree(blocks).Select(ToResponse).ToList());
    }

    public async Task<ServiceResult<BlockResponse>> CreateAsync(Guid pageId, CreateBlockRequest request, CancellationToken ct)
    {
        var info = await PageAuthInfoAsync(pageId, ct);
        if (info is null)
        {
            return ServiceResult<BlockResponse>.NotFound("Page not found.");
        }

        if (Guard<BlockResponse>(PageAuthorization.CanWrite(Member, info.WorkspaceId, info.Visibility, info.Permission)) is { } denied)
        {
            return denied;
        }

        if (request.Content is not { ValueKind: JsonValueKind.Object } content)
        {
            return ServiceResult<BlockResponse>.Invalid("Block content must be a JSON object.");
        }

        if (await ValidateParentAsync(pageId, request.ParentId, ct) is { } parentError)
        {
            return ServiceResult<BlockResponse>.Invalid(parentError);
        }

        var siblings = await OrderedBlocksAsync(pageId, request.ParentId, excluding: null, ct);
        var insertAt = Clamp(request.Position ?? siblings.Count, 0, siblings.Count);

        var block = new Block
        {
            Id = Guid.NewGuid(),
            PageId = pageId,
            ParentBlockId = request.ParentId,
            Type = request.Type!.Value,
            Position = insertAt,
            Content = content.GetRawText(),
            CreatedAt = Now,
            UpdatedAt = Now,
        };

        siblings.Insert(insertAt, block);
        Reindex(siblings);
        db.Blocks.Add(block);
        await TouchPageAsync(pageId, ct);
        await db.SaveChangesAsync(ct);

        return ServiceResult<BlockResponse>.Ok(ToResponse(block));
    }

    public async Task<ServiceResult<BlockResponse>> UpdateAsync(Guid blockId, UpdateBlockRequest request, CancellationToken ct)
    {
        var block = await db.Blocks.FirstOrDefaultAsync(b => b.Id == blockId, ct);
        if (block is null)
        {
            return ServiceResult<BlockResponse>.NotFound("Block not found.");
        }

        if (await WriteGuardAsync<BlockResponse>(block.PageId, ct) is { } denied)
        {
            return denied;
        }

        if (request.Content is not { ValueKind: JsonValueKind.Object } content)
        {
            return ServiceResult<BlockResponse>.Invalid("Block content must be a JSON object.");
        }

        block.Content = content.GetRawText();
        if (request.Type is BlockType type)
        {
            block.Type = type;
        }

        block.UpdatedAt = Now;
        await TouchPageAsync(block.PageId, ct);
        await db.SaveChangesAsync(ct);

        return ServiceResult<BlockResponse>.Ok(ToResponse(block));
    }

    public async Task<ServiceResult<BlockResponse>> MoveAsync(Guid blockId, MoveBlockRequest request, CancellationToken ct)
    {
        var block = await db.Blocks.FirstOrDefaultAsync(b => b.Id == blockId, ct);
        if (block is null)
        {
            return ServiceResult<BlockResponse>.NotFound("Block not found.");
        }

        if (await WriteGuardAsync<BlockResponse>(block.PageId, ct) is { } denied)
        {
            return denied;
        }

        var targetParentId = request.ParentId;
        if (targetParentId is Guid tp)
        {
            if (tp == blockId)
            {
                return ServiceResult<BlockResponse>.Invalid("A block cannot be its own parent.");
            }

            if (await ValidateParentAsync(block.PageId, tp, ct) is { } parentError)
            {
                return ServiceResult<BlockResponse>.Invalid(parentError);
            }

            var descendants = await DescendantBlockIdsAsync(block.PageId, blockId, ct);
            if (descendants.Contains(tp))
            {
                return ServiceResult<BlockResponse>.Invalid("Cannot move a block into its own descendant.");
            }
        }

        var oldParentId = block.ParentBlockId;
        block.ParentBlockId = targetParentId;

        var targetSiblings = await OrderedBlocksAsync(block.PageId, targetParentId, excluding: blockId, ct);
        var insertAt = Clamp(request.Position, 0, targetSiblings.Count);
        targetSiblings.Insert(insertAt, block);
        Reindex(targetSiblings);

        if (oldParentId != targetParentId)
        {
            var oldSiblings = await OrderedBlocksAsync(block.PageId, oldParentId, excluding: blockId, ct);
            Reindex(oldSiblings);
        }

        block.UpdatedAt = Now;
        await TouchPageAsync(block.PageId, ct);
        await db.SaveChangesAsync(ct);

        return ServiceResult<BlockResponse>.Ok(ToResponse(block));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid blockId, CancellationToken ct)
    {
        var block = await db.Blocks.FirstOrDefaultAsync(b => b.Id == blockId, ct);
        if (block is null)
        {
            return ServiceResult<bool>.NotFound("Block not found.");
        }

        if (await WriteGuardAsync<bool>(block.PageId, ct) is { } denied)
        {
            return denied;
        }

        var pageId = block.PageId;
        var parentId = block.ParentBlockId;

        // Deleting a block removes its whole subtree (children under a Toggle).
        var subtree = await DescendantBlockIdsAsync(pageId, blockId, ct);
        subtree.Add(blockId);
        var toRemove = await db.Blocks.Where(b => subtree.Contains(b.Id)).ToListAsync(ct);
        db.Blocks.RemoveRange(toRemove);
        await db.SaveChangesAsync(ct);

        var remaining = await OrderedBlocksAsync(pageId, parentId, excluding: null, ct);
        Reindex(remaining);
        await TouchPageAsync(pageId, ct);
        await db.SaveChangesAsync(ct);

        return ServiceResult<bool>.Ok(true);
    }

    // ---- authorization helpers ----

    /// <summary>Minimal page fields needed to authorize a block operation.</summary>
    private sealed record PageAuthInfo(Guid WorkspaceId, PageVisibility Visibility, SharePermission Permission);

    private Task<PageAuthInfo?> PageAuthInfoAsync(Guid pageId, CancellationToken ct) =>
        db.Pages
            .Where(p => p.Id == pageId)
            .Select(p => new PageAuthInfo(p.WorkspaceId, p.Visibility, p.Permission))
            .FirstOrDefaultAsync(ct);

    /// <summary>Loads the block's page and denies (403/404) if the caller can't write it; null when allowed.</summary>
    private async Task<ServiceResult<T>?> WriteGuardAsync<T>(Guid pageId, CancellationToken ct)
    {
        var info = await PageAuthInfoAsync(pageId, ct);
        if (info is null)
        {
            return ServiceResult<T>.NotFound("Page not found.");
        }

        return Guard<T>(PageAuthorization.CanWrite(Member, info.WorkspaceId, info.Visibility, info.Permission));
    }

    /// <summary>Maps an access result to a denial ServiceResult, or null when allowed.</summary>
    private static ServiceResult<T>? Guard<T>(AccessResult access) => access switch
    {
        AccessResult.Allowed => null,
        AccessResult.Forbidden => ServiceResult<T>.Forbidden("You don't have permission to modify this page."),
        _ => ServiceResult<T>.NotFound("Page not found."),
    };

    // ---- helpers ----

    /// <summary>The ordered sibling blocks sharing a parent (null = page root).</summary>
    private async Task<List<Block>> OrderedBlocksAsync(Guid pageId, Guid? parentId, Guid? excluding, CancellationToken ct)
    {
        var query = db.Blocks.Where(b => b.PageId == pageId && b.ParentBlockId == parentId);
        if (excluding is Guid id)
        {
            query = query.Where(b => b.Id != id);
        }

        return await query.OrderBy(b => b.Position).ToListAsync(ct);
    }

    /// <summary>Null when a parent id is valid (a Toggle on the same page); otherwise an error message.</summary>
    private async Task<string?> ValidateParentAsync(Guid pageId, Guid? parentId, CancellationToken ct)
    {
        if (parentId is not Guid id)
        {
            return null;
        }

        var parent = await db.Blocks
            .Where(b => b.Id == id && b.PageId == pageId)
            .Select(b => new { b.Type })
            .FirstOrDefaultAsync(ct);

        if (parent is null)
        {
            return "Parent block not found on this page.";
        }

        return parent.Type == BlockType.Toggle
            ? null
            : "Only Toggle blocks can contain child blocks.";
    }

    /// <summary>Ids of every block beneath <paramref name="rootId"/> within the page.</summary>
    private async Task<HashSet<Guid>> DescendantBlockIdsAsync(Guid pageId, Guid rootId, CancellationToken ct)
    {
        var edges = await db.Blocks
            .Where(b => b.PageId == pageId)
            .Select(b => new { b.Id, b.ParentBlockId })
            .ToListAsync(ct);

        var childrenByParent = edges
            .Where(e => e.ParentBlockId is not null)
            .GroupBy(e => e.ParentBlockId!.Value)
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

    /// <summary>Flattens the block set into pre-order DFS: parent, then its ordered children.</summary>
    private static List<Block> OrderTree(List<Block> blocks)
    {
        var byParent = blocks.OrderBy(b => b.Position).ToLookup(b => b.ParentBlockId);
        var result = new List<Block>(blocks.Count);

        void Visit(Guid? parentId)
        {
            foreach (var block in byParent[parentId])
            {
                result.Add(block);
                Visit(block.Id);
            }
        }

        Visit(null);
        return result;
    }

    private async Task TouchPageAsync(Guid pageId, CancellationToken ct)
    {
        var page = await db.Pages.FirstOrDefaultAsync(p => p.Id == pageId, ct);
        if (page is not null)
        {
            page.UpdatedAt = Now;
        }
    }

    private static void Reindex(List<Block> ordered)
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

    private static BlockResponse ToResponse(Block block) => new(
        block.Id,
        block.PageId,
        block.ParentBlockId,
        block.Type,
        block.Position,
        JsonSerializer.Deserialize<JsonElement>(block.Content),
        block.CreatedAt,
        block.UpdatedAt);
}
