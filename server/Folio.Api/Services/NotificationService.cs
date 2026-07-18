using Folio.Api.Auth;
using Folio.Api.Contracts;
using Folio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Folio.Api.Services;

/// <summary>Reads a member's notifications and a workspace's activity feed.</summary>
public class NotificationService(FolioDbContext db, ICurrentMemberAccessor current)
{
    private CurrentMember? Member => current.Member;

    public async Task<IReadOnlyList<NotificationResponse>> GetForMemberAsync(CancellationToken ct)
    {
        var member = Member;
        if (member is null)
        {
            return [];
        }

        // Capped for the pagination audit — the inbox shows the most recent 200.
        return await db.Notifications
            .Where(n => n.RecipientMemberId == member.MemberId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(200)
            .Select(n => new NotificationResponse(n.Id, n.Type, n.PageId, n.PageTitle, n.Summary, n.IsRead, n.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<int> UnreadCountAsync(CancellationToken ct)
    {
        var member = Member;
        return member is null
            ? 0
            : await db.Notifications.CountAsync(n => n.RecipientMemberId == member.MemberId && !n.IsRead, ct);
    }

    /// <summary>Marks one of the member's own notifications read. Returns false if not theirs/absent.</summary>
    public async Task<bool> MarkReadAsync(Guid notificationId, CancellationToken ct)
    {
        var member = Member;
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId, ct);
        if (member is null || notification is null || notification.RecipientMemberId != member.MemberId)
        {
            return false;
        }

        notification.IsRead = true;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> MarkAllReadAsync(CancellationToken ct)
    {
        var member = Member;
        if (member is null)
        {
            return 0;
        }

        var unread = await db.Notifications
            .Where(n => n.RecipientMemberId == member.MemberId && !n.IsRead)
            .ToListAsync(ct);
        foreach (var n in unread)
        {
            n.IsRead = true;
        }
        await db.SaveChangesAsync(ct);
        return unread.Count;
    }

    /// <summary>Recent activity for a workspace (the caller's own), or null if foreign.</summary>
    public async Task<IReadOnlyList<ActivityResponse>?> GetWorkspaceActivityAsync(Guid workspaceId, int limit, CancellationToken ct)
    {
        if (Member?.WorkspaceId != workspaceId)
        {
            return null;
        }

        return await db.Activities
            .Where(a => a.WorkspaceId == workspaceId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(Math.Clamp(limit, 1, 200))
            .Select(a => new ActivityResponse(a.Id, a.ActorName, a.Type, a.PageId, a.PageTitle, a.Summary, a.CreatedAt))
            .ToListAsync(ct);
    }
}
