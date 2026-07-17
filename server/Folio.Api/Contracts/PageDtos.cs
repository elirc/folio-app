using System.ComponentModel.DataAnnotations;
using Folio.Domain.Enums;

namespace Folio.Api.Contracts;

/// <summary>A node in the nested page tree returned for the sidebar.</summary>
public record PageTreeNode(
    Guid Id,
    Guid? ParentId,
    string Title,
    string? Icon,
    int Position,
    bool IsFavorite,
    IReadOnlyList<PageTreeNode> Children);

/// <summary>A single ancestor entry for breadcrumbs.</summary>
public record BreadcrumbItem(Guid Id, string Title, string? Icon);

/// <summary>Full page detail including its breadcrumb trail (root → self).</summary>
public record PageDetailResponse(
    Guid Id,
    Guid WorkspaceId,
    Guid? ParentId,
    string Title,
    string? Icon,
    int Position,
    PageVisibility Visibility,
    SharePermission Permission,
    string? PublicSlug,
    bool IsFavorite,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<BreadcrumbItem> Breadcrumb);

// Validation attributes stay on the record's constructor parameters (no
// [property:] target): ASP.NET Core reads validation metadata from the primary
// constructor parameters for records, and .NET 10 throws if it finds validation
// attributes on the generated properties instead. Required fields are declared
// nullable so a missing value fails validation (400) rather than binding a
// bogus default.

/// <summary>Create a page. <c>ParentId</c> null means a root page.</summary>
public record CreatePageRequest(
    [Required][MaxLength(400)] string? Title,
    Guid? ParentId,
    int? Position,
    [MaxLength(40)] string? Icon);

/// <summary>Rename a page and/or change its icon.</summary>
public record UpdatePageRequest(
    [Required][MaxLength(400)] string? Title,
    [MaxLength(40)] string? Icon);

/// <summary>Move/reorder a page under a new parent (null = root) at a position.</summary>
public record MovePageRequest(Guid? ParentId, int Position);
