using Folio.Api.Auth;
using Folio.Domain.Entities;
using Folio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Folio.Api.Services;

/// <summary>Activity event types.</summary>
public static class ActivityTypes
{
    public const string PageCreated = "PageCreated";
    public const string PageUpdated = "PageUpdated";
    public const string PageDeleted = "PageDeleted";
    public const string BlockCreated = "BlockCreated";
    public const string BlockUpdated = "BlockUpdated";
    public const string BlockDeleted = "BlockDeleted";
    public const string CommentCreated = "CommentCreated";
}

/// <summary>
/// Records activity entries and fans notifications out from them. Entities are
/// only added to the context — the calling service's SaveChanges persists them,
/// so activity/notification writes are part of the same transaction as the mutation.
/// </summary>
public class ActivityService(FolioDbContext db)
{
    private static DateTime Now => DateTime.UtcNow;

    /// <summary>Adds an activity entry to the context (not yet saved).</summary>
    public Activity Add(
        Guid workspaceId,
        CurrentMember actor,
        string type,
        Guid? pageId,
        string? pageTitle,
        string summary,
        Guid? commentId = null)
    {
        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ActorMemberId = actor.MemberId,
            ActorName = actor.Name,
            Type = type,
            PageId = pageId,
            PageTitle = pageTitle,
            CommentId = commentId,
            Summary = summary,
            CreatedAt = Now,
        };
        db.Activities.Add(activity);
        return activity;
    }

    /// <summary>
    /// Creates notifications for a comment activity: the mentioned members, the
    /// page author, and prior commenters on the page (excluding the actor).
    /// </summary>
    public async Task FanOutCommentAsync(
        Activity activity,
        Guid pageId,
        Guid? pageAuthorId,
        Guid actorId,
        IEnumerable<Guid> mentionedIds,
        CancellationToken ct)
    {
        var recipients = new HashSet<Guid>(mentionedIds);
        if (pageAuthorId is Guid author)
        {
            recipients.Add(author);
        }

        // Prior commenters are already persisted, so they don't include this comment.
        var priorAuthors = await db.Comments
            .Where(c => c.PageId == pageId)
            .Select(c => c.AuthorMemberId)
            .Distinct()
            .ToListAsync(ct);
        recipients.UnionWith(priorAuthors);

        recipients.Remove(actorId);

        foreach (var recipient in recipients)
        {
            db.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                RecipientMemberId = recipient,
                ActivityId = activity.Id,
                Type = activity.Type,
                PageId = activity.PageId,
                PageTitle = activity.PageTitle,
                Summary = activity.Summary,
                IsRead = false,
                CreatedAt = Now,
            });
        }
    }
}
