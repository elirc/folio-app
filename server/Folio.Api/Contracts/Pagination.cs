using System.ComponentModel.DataAnnotations;

namespace Folio.Api.Contracts;

/// <summary>Query-string pagination parameters (validated by [ApiController]).</summary>
public class PaginationQuery
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

/// <summary>A page of results with paging metadata.</summary>
public record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int Total,
    int TotalPages);

/// <summary>A flat page-list row with a block count and a short preview.</summary>
public record PageListItemResponse(
    Guid Id,
    string Title,
    string? Icon,
    int BlockCount,
    string? Preview,
    DateTime UpdatedAt);
