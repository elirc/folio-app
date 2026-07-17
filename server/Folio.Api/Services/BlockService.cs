using System.Text.Json;
using Folio.Api.Contracts;
using Folio.Domain.Entities;
using Folio.Domain.Enums;
using Folio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Folio.Api.Services;

/// <summary>Typed content-block operations, ordered per page.</summary>
public class BlockService(FolioDbContext db)
{
    private static DateTime Now => DateTime.UtcNow;

    /// <summary>Blocks for a page in order, or null if the page does not exist.</summary>
    public async Task<IReadOnlyList<BlockResponse>?> GetForPageAsync(Guid pageId, CancellationToken ct)
    {
        if (!await db.Pages.AnyAsync(p => p.Id == pageId, ct))
        {
            return null;
        }

        var blocks = await db.Blocks
            .Where(b => b.PageId == pageId)
            .OrderBy(b => b.Position)
            .ToListAsync(ct);

        return blocks.Select(ToResponse).ToList();
    }

    public async Task<ServiceResult<BlockResponse>> CreateAsync(Guid pageId, CreateBlockRequest request, CancellationToken ct)
    {
        if (!await db.Pages.AnyAsync(p => p.Id == pageId, ct))
        {
            return ServiceResult<BlockResponse>.NotFound("Page not found.");
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

        var pageId = block.PageId;
        db.Blocks.Remove(block);
        await db.SaveChangesAsync(ct);

        var remaining = await OrderedBlocksAsync(pageId, excluding: null, ct);
        Reindex(remaining);
        await TouchPageAsync(pageId, ct);
        await db.SaveChangesAsync(ct);

        return ServiceResult<bool>.Ok(true);
    }

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
