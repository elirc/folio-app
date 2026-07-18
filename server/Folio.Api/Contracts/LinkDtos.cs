namespace Folio.Api.Contracts;

/// <summary>A page that links to the current page (an inbound reference).</summary>
public record BacklinkResponse(
    Guid SourcePageId,
    string SourcePageTitle,
    string? SourcePageIcon,
    Guid SourceBlockId);

/// <summary>An outbound link from the current page; <c>IsBroken</c> when the target is gone/trashed.</summary>
public record OutgoingLinkResponse(
    Guid TargetPageId,
    string TargetTitle,
    bool IsBroken,
    Guid SourceBlockId);
