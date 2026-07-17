using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Folio.Domain.Enums;

namespace Folio.Api.Contracts;

/// <summary>A typed content block. <c>Content</c> is a JSON object payload.</summary>
public record BlockResponse(
    Guid Id,
    Guid PageId,
    Guid? ParentBlockId,
    BlockType Type,
    int Position,
    JsonElement Content,
    DateTime CreatedAt,
    DateTime UpdatedAt);

// Attributes live on the constructor parameters (records). Type and Content are
// nullable so a missing value fails validation with 400 — [Required] on a
// non-nullable enum/struct would be a no-op.

/// <summary>Create a block; appended unless <c>Position</c> is given. <c>ParentId</c> nests under a Toggle.</summary>
public record CreateBlockRequest(
    [Required] BlockType? Type,
    [Required] JsonElement? Content,
    int? Position,
    Guid? ParentId);

/// <summary>Update a block's payload and/or change its type.</summary>
public record UpdateBlockRequest(
    BlockType? Type,
    [Required] JsonElement? Content);

/// <summary>Reorder a block within/between parents. <c>ParentId</c> null = page root.</summary>
public record MoveBlockRequest(int Position, Guid? ParentId);
