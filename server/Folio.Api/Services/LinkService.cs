using Folio.Api.Auth;
using Folio.Api.Contracts;
using Folio.Domain.Enums;
using Folio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Folio.Api.Services;

/// <summary>Reads inline link references: who links here (backlinks) and what a page links to.</summary>
public class LinkService(FolioDbContext db, ICurrentMemberAccessor current)
{
    private CurrentMember? Member => current.Member;

    /// <summary>Pages that link to the given page. Trashed source pages are excluded automatically.</summary>
    public async Task<ServiceResult<IReadOnlyList<BacklinkResponse>>> GetBacklinksAsync(Guid pageId, CancellationToken ct)
    {
        var target = await PageAuthAsync(pageId, ct);
        if (target is null)
        {
            return ServiceResult<IReadOnlyList<BacklinkResponse>>.NotFound("Page not found.");
        }

        if (ReadGuard<IReadOnlyList<BacklinkResponse>>(target.WorkspaceId, target.Visibility) is { } denied)
        {
            return denied;
        }

        var member = Member!;

        // Join links to live pages (the query filter hides trashed source pages),
        // keeping only same-workspace sources the caller may see.
        var backlinks = await (
            from link in db.PageLinks.Where(l => l.TargetPageId == pageId)
            join source in db.Pages on link.SourcePageId equals source.Id
            where source.WorkspaceId == target.WorkspaceId
            select new
            {
                link.SourcePageId,
                source.Title,
                source.Icon,
                source.Visibility,
                link.SourceBlockId,
            }).ToListAsync(ct);

        var result = backlinks
            .Where(b => PageAuthorization.CanSeeVisibility(member, b.Visibility))
            .Select(b => new BacklinkResponse(b.SourcePageId, b.Title, b.Icon, b.SourceBlockId))
            .ToList();

        return ServiceResult<IReadOnlyList<BacklinkResponse>>.Ok(result);
    }

    /// <summary>Links leaving the given page; each is flagged broken when its target is gone/trashed.</summary>
    public async Task<ServiceResult<IReadOnlyList<OutgoingLinkResponse>>> GetOutgoingAsync(Guid pageId, CancellationToken ct)
    {
        var source = await PageAuthAsync(pageId, ct);
        if (source is null)
        {
            return ServiceResult<IReadOnlyList<OutgoingLinkResponse>>.NotFound("Page not found.");
        }

        if (ReadGuard<IReadOnlyList<OutgoingLinkResponse>>(source.WorkspaceId, source.Visibility) is { } denied)
        {
            return denied;
        }

        var member = Member!;

        var links = await db.PageLinks
            .Where(l => l.SourcePageId == pageId)
            .Select(l => new { l.TargetPageId, l.TargetTitle, l.SourceBlockId })
            .ToListAsync(ct);

        // A target resolves to its live title only when it exists, is not trashed
        // (query filter), and the caller is allowed to see it. A target the caller
        // can't see (e.g. a page since made private) is reported as broken so its
        // current title never leaks — the stored token title, which is already
        // present in the source block's own visible text, is shown instead. This
        // mirrors the visibility filtering already applied to backlinks.
        var liveTargets = await db.Pages
            .Where(p => links.Select(l => l.TargetPageId).Contains(p.Id))
            .Select(p => new { p.Id, p.Title, p.Visibility })
            .ToListAsync(ct);
        var liveById = liveTargets
            .Where(p => PageAuthorization.CanSeeVisibility(member, p.Visibility))
            .ToDictionary(p => p.Id, p => p.Title);

        var result = links
            .Select(l => liveById.TryGetValue(l.TargetPageId, out var title)
                ? new OutgoingLinkResponse(l.TargetPageId, title, false, l.SourceBlockId)
                : new OutgoingLinkResponse(l.TargetPageId, l.TargetTitle, true, l.SourceBlockId))
            .ToList();

        return ServiceResult<IReadOnlyList<OutgoingLinkResponse>>.Ok(result);
    }

    private sealed record PageAuth(Guid WorkspaceId, PageVisibility Visibility);

    private Task<PageAuth?> PageAuthAsync(Guid pageId, CancellationToken ct) =>
        db.Pages
            .Where(p => p.Id == pageId)
            .Select(p => new PageAuth(p.WorkspaceId, p.Visibility))
            .FirstOrDefaultAsync(ct);

    private ServiceResult<T>? ReadGuard<T>(Guid workspaceId, PageVisibility visibility) =>
        PageAuthorization.CanRead(Member, workspaceId, visibility) switch
        {
            AccessResult.Allowed => null,
            AccessResult.Forbidden => ServiceResult<T>.Forbidden("You don't have access to this page."),
            _ => ServiceResult<T>.NotFound("Page not found."),
        };
}
