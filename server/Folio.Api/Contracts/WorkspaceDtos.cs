namespace Folio.Api.Contracts;

/// <summary>Summary view of a workspace with member and page counts.</summary>
public record WorkspaceSummaryResponse(
    Guid Id,
    string Name,
    string Slug,
    int MemberCount,
    int PageCount,
    DateTime CreatedAt);
