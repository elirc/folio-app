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
            .OrderBy(b => b.Position)
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<BlockResponse>>.Ok(blocks.Select(ToResponse).ToList());
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

        var siblings = await OrderedBlocksAsync(pageId, excluding: null, ct);
        var insertAt = Clamp(request.Position ?? siblings.Count, 0, siblings.Count);

        var block = new Block
        {
            Id = Guid.NewGuid(),
            PageId = pageId,
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

        var siblings = await OrderedBlocksAsync(block.PageId, excluding: blockId, ct);
        var insertAt = Clamp(request.Position, 0, siblings.Count);
        siblings.Insert(insertAt, block);
        Reindex(siblings);

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
        db.Blocks.Remove(block);
        await db.SaveChangesAsync(ct);

        var remaining = await OrderedBlocksAsync(pageId, excluding: null, ct);
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

    private async Task<List<Block>> OrderedBlocksAsync(Guid pageId, Guid? excluding, CancellationToken ct)
    {
        var query = db.Blocks.Where(b => b.PageId == pageId);
        if (excluding is Guid id)
        {
            query = query.Where(b => b.Id != id);
        }

        return await query.OrderBy(b => b.Position).ToListAsync(ct);
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
        block.Type,
        block.Position,
        JsonSerializer.Deserialize<JsonElement>(block.Content),
        block.CreatedAt,
        block.UpdatedAt);
}
