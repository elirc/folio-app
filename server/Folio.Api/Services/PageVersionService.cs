using System.Text.Json;
using Folio.Api.Auth;
using Folio.Api.Contracts;
using Folio.Domain.Entities;
using Folio.Domain.Enums;
using Folio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Folio.Api.Services;

/// <summary>
/// Append-only page history: snapshots of a page's title/icon and block set, a
/// diff summary against the current page, and non-destructive restore.
/// </summary>
public class PageVersionService(FolioDbContext db, ICurrentMemberAccessor current)
{
    private static DateTime Now => DateTime.UtcNow;
    private CurrentMember? Member => current.Member;

    /// <summary>The block set as stored inside a version's JSON snapshot.</summary>
    private sealed record StoredBlock(Guid Id, Guid? ParentBlockId, string Type, int Position, string Content);

    /// <summary>Capture the page's current state as a new version.</summary>
    public async Task<ServiceResult<VersionSummaryResponse>> SnapshotAsync(Guid pageId, CancellationToken ct)
    {
        var page = await db.Pages.FirstOrDefaultAsync(p => p.Id == pageId, ct);
        if (page is null)
        {
            return ServiceResult<VersionSummaryResponse>.NotFound("Page not found.");
        }

        if (WriteGuard<VersionSummaryResponse>(page.WorkspaceId, page.Visibility, page.Permission) is { } denied)
        {
            return denied;
        }

        var version = await SnapshotInternalAsync(page, label: null, ct);
        return ServiceResult<VersionSummaryResponse>.Ok(ToSummary(version));
    }

    /// <summary>List a page's versions, newest first.</summary>
    public async Task<ServiceResult<IReadOnlyList<VersionSummaryResponse>>> ListAsync(Guid pageId, CancellationToken ct)
    {
        var page = await PageAuthAsync(pageId, ct);
        if (page is null)
        {
            return ServiceResult<IReadOnlyList<VersionSummaryResponse>>.NotFound("Page not found.");
        }

        if (ReadGuard<IReadOnlyList<VersionSummaryResponse>>(page.WorkspaceId, page.Visibility) is { } denied)
        {
            return denied;
        }

        // Capped for the pagination audit — the most recent 200 versions.
        var versions = await db.PageVersions
            .Where(v => v.PageId == pageId)
            .OrderByDescending(v => v.VersionNumber)
            .Take(200)
            .Select(v => new VersionSummaryResponse(
                v.VersionNumber, v.Title, v.Icon, v.BlockCount, v.CreatedByName, v.Label, v.CreatedAt))
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<VersionSummaryResponse>>.Ok(versions);
    }

    /// <summary>A single version's full snapshot plus a diff against the current page.</summary>
    public async Task<ServiceResult<VersionDetailResponse>> GetAsync(Guid pageId, int versionNumber, CancellationToken ct)
    {
        var page = await PageAuthAsync(pageId, ct);
        if (page is null)
        {
            return ServiceResult<VersionDetailResponse>.NotFound("Page not found.");
        }

        if (ReadGuard<VersionDetailResponse>(page.WorkspaceId, page.Visibility) is { } denied)
        {
            return denied;
        }

        var version = await db.PageVersions
            .FirstOrDefaultAsync(v => v.PageId == pageId && v.VersionNumber == versionNumber, ct);
        if (version is null)
        {
            return ServiceResult<VersionDetailResponse>.NotFound("Version not found.");
        }

        var stored = Deserialize(version.BlocksJson);
        var diff = await DiffAgainstCurrentAsync(pageId, stored, ct);
        var blocks = stored
            .Select(b => new BlockSnapshotDto(
                b.Id, b.ParentBlockId, Enum.Parse<BlockType>(b.Type), b.Position, Parse(b.Content)))
            .ToList();

        return ServiceResult<VersionDetailResponse>.Ok(new VersionDetailResponse(
            version.VersionNumber, version.Title, version.Icon, blocks, diff,
            version.CreatedByName, version.Label, version.CreatedAt));
    }

    /// <summary>
    /// Restore a version's title/icon + block set. The current state is first
    /// snapshotted into a new version, so restore is never destructive.
    /// </summary>
    public async Task<ServiceResult<VersionSummaryResponse>> RestoreAsync(Guid pageId, int versionNumber, CancellationToken ct)
    {
        var page = await db.Pages.FirstOrDefaultAsync(p => p.Id == pageId, ct);
        if (page is null)
        {
            return ServiceResult<VersionSummaryResponse>.NotFound("Page not found.");
        }

        if (WriteGuard<VersionSummaryResponse>(page.WorkspaceId, page.Visibility, page.Permission) is { } denied)
        {
            return denied;
        }

        var target = await db.PageVersions
            .FirstOrDefaultAsync(v => v.PageId == pageId && v.VersionNumber == versionNumber, ct);
        if (target is null)
        {
            return ServiceResult<VersionSummaryResponse>.NotFound("Version not found.");
        }

        // 1. Preserve the current state as a new version (non-destructive).
        var preRestore = await SnapshotInternalAsync(page, label: $"Before restore to v{versionNumber}", ct);

        // 2. Replace the page's blocks with the target snapshot (reusing ids keeps
        //    parent references intact).
        var current = await db.Blocks.Where(b => b.PageId == pageId).ToListAsync(ct);
        db.Blocks.RemoveRange(current);
        await db.SaveChangesAsync(ct);

        var stored = Deserialize(target.BlocksJson);
        var restored = stored.Select(b => new Block
        {
            Id = b.Id,
            PageId = pageId,
            ParentBlockId = b.ParentBlockId,
            Type = Enum.Parse<BlockType>(b.Type),
            Position = b.Position,
            Content = b.Content,
            CreatedAt = Now,
            UpdatedAt = Now,
        }).ToList();
        db.Blocks.AddRange(restored);

        page.Title = target.Title;
        page.Icon = target.Icon;
        page.UpdatedAt = Now;
        await db.SaveChangesAsync(ct);

        return ServiceResult<VersionSummaryResponse>.Ok(ToSummary(preRestore));
    }

    // ---- snapshot / diff helpers ----

    private async Task<PageVersion> SnapshotInternalAsync(Page page, string? label, CancellationToken ct)
    {
        var blocks = await db.Blocks
            .Where(b => b.PageId == page.Id)
            .OrderBy(b => b.Position)
            .ToListAsync(ct);

        var stored = blocks
            .Select(b => new StoredBlock(b.Id, b.ParentBlockId, b.Type.ToString(), b.Position, b.Content))
            .ToList();

        var nextNumber = ((await db.PageVersions
            .Where(v => v.PageId == page.Id)
            .Select(v => (int?)v.VersionNumber)
            .MaxAsync(ct)) ?? 0) + 1;

        var version = new PageVersion
        {
            Id = Guid.NewGuid(),
            PageId = page.Id,
            VersionNumber = nextNumber,
            Title = page.Title,
            Icon = page.Icon,
            BlocksJson = JsonSerializer.Serialize(stored),
            BlockCount = stored.Count,
            CreatedByMemberId = Member?.MemberId,
            CreatedByName = Member?.Name,
            Label = label,
            CreatedAt = Now,
        };

        db.PageVersions.Add(version);
        await db.SaveChangesAsync(ct);
        return version;
    }

    private async Task<DiffSummary> DiffAgainstCurrentAsync(Guid pageId, List<StoredBlock> versionBlocks, CancellationToken ct)
    {
        var current = await db.Blocks
            .Where(b => b.PageId == pageId)
            .Select(b => new StoredBlock(b.Id, b.ParentBlockId, b.Type.ToString(), b.Position, b.Content))
            .ToListAsync(ct);

        var currentById = current.ToDictionary(b => b.Id);
        var versionById = versionBlocks.ToDictionary(b => b.Id);

        // Added = present now but not in the version; removed = in the version but gone now.
        var added = current.Count(b => !versionById.ContainsKey(b.Id));
        var removed = versionBlocks.Count(b => !currentById.ContainsKey(b.Id));
        var changed = versionBlocks.Count(v =>
            currentById.TryGetValue(v.Id, out var c)
            && (c.Type != v.Type || c.Position != v.Position || c.ParentBlockId != v.ParentBlockId || c.Content != v.Content));

        return new DiffSummary(added, removed, changed);
    }

    private static List<StoredBlock> Deserialize(string json) =>
        JsonSerializer.Deserialize<List<StoredBlock>>(json) ?? [];

    private static JsonElement Parse(string content) => JsonSerializer.Deserialize<JsonElement>(content);

    private static VersionSummaryResponse ToSummary(PageVersion v) =>
        new(v.VersionNumber, v.Title, v.Icon, v.BlockCount, v.CreatedByName, v.Label, v.CreatedAt);

    // ---- authorization helpers ----

    private sealed record PageAuth(Guid WorkspaceId, PageVisibility Visibility, SharePermission Permission);

    private Task<PageAuth?> PageAuthAsync(Guid pageId, CancellationToken ct) =>
        db.Pages
            .Where(p => p.Id == pageId)
            .Select(p => new PageAuth(p.WorkspaceId, p.Visibility, p.Permission))
            .FirstOrDefaultAsync(ct);

    private ServiceResult<T>? ReadGuard<T>(Guid workspaceId, PageVisibility visibility) =>
        PageAuthorization.CanRead(Member, workspaceId, visibility) switch
        {
            AccessResult.Allowed => null,
            AccessResult.Forbidden => ServiceResult<T>.Forbidden("You don't have access to this page."),
            _ => ServiceResult<T>.NotFound("Page not found."),
        };

    private ServiceResult<T>? WriteGuard<T>(Guid workspaceId, PageVisibility visibility, SharePermission permission) =>
        PageAuthorization.CanWrite(Member, workspaceId, visibility, permission) switch
        {
            AccessResult.Allowed => null,
            AccessResult.Forbidden => ServiceResult<T>.Forbidden("You don't have permission to modify this page."),
            _ => ServiceResult<T>.NotFound("Page not found."),
        };
}
