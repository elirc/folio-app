using System.ComponentModel.DataAnnotations;
using Folio.Domain.Enums;

namespace Folio.Api.Contracts;

/// <summary>Set a page's visibility and access level.</summary>
public record ShareRequest(
    [Required] PageVisibility? Visibility,
    [Required] SharePermission? Permission);

/// <summary>Current sharing settings for a page (with public slug if any).</summary>
public record ShareResponse(
    PageVisibility Visibility,
    SharePermission Permission,
    string? PublicSlug);

/// <summary>A search hit over page titles and block text.</summary>
public record SearchResultResponse(
    Guid PageId,
    string Title,
    string? Icon,
    bool MatchedTitle,
    string? Snippet);

/// <summary>A page currently in the trash.</summary>
public record TrashItemResponse(
    Guid Id,
    string Title,
    string? Icon,
    DateTime? DeletedAt);

/// <summary>A page marked as a favorite.</summary>
public record FavoriteResponse(
    Guid Id,
    string Title,
    string? Icon);
