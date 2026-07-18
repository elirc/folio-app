namespace Folio.Api.Contracts;

/// <summary>A workspace activity-feed entry.</summary>
public record ActivityResponse(
    Guid Id,
    string ActorName,
    string Type,
    Guid? PageId,
    string? PageTitle,
    string Summary,
    DateTime CreatedAt);

/// <summary>A per-user notification.</summary>
public record NotificationResponse(
    Guid Id,
    string Type,
    Guid? PageId,
    string? PageTitle,
    string Summary,
    bool IsRead,
    DateTime CreatedAt);

/// <summary>Unread-notification count.</summary>
public record UnreadCountResponse(int Count);
