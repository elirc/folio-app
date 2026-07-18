namespace Folio.Domain.Entities;

/// <summary>
/// An append-only record of a mutation (page/block/comment) used for the workspace
/// activity feed and as the source that notifications fan out from.
/// </summary>
public class Activity
{
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }

    public Guid ActorMemberId { get; set; }
    public string ActorName { get; set; } = string.Empty;

    /// <summary>Event type, e.g. PageCreated, BlockUpdated, CommentCreated.</summary>
    public string Type { get; set; } = string.Empty;

    public Guid? PageId { get; set; }
    public string? PageTitle { get; set; }
    public Guid? CommentId { get; set; }

    public string Summary { get; set; } = string.Empty;

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>A per-recipient notification fanned out from an <see cref="Activity"/>.</summary>
public class Notification
{
    public Guid Id { get; set; }

    public Guid RecipientMemberId { get; set; }

    public Guid ActivityId { get; set; }
    public Activity? Activity { get; set; }

    public string Type { get; set; } = string.Empty;
    public Guid? PageId { get; set; }
    public string? PageTitle { get; set; }
    public string Summary { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }
}
