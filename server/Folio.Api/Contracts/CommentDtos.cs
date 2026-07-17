using System.ComponentModel.DataAnnotations;

namespace Folio.Api.Contracts;

/// <summary>A member referenced by an @mention in a comment.</summary>
public record MentionDto(Guid MemberId, string Name);

/// <summary>A comment on a page or block, with its parsed mentions.</summary>
public record CommentResponse(
    Guid Id,
    Guid PageId,
    Guid? BlockId,
    Guid? ParentCommentId,
    Guid AuthorMemberId,
    string AuthorName,
    string Body,
    bool IsResolved,
    DateTime? ResolvedAt,
    IReadOnlyList<MentionDto> Mentions,
    DateTime CreatedAt,
    DateTime UpdatedAt);

// Required fields are nullable so a missing value fails validation (400).

/// <summary>Create a comment. <c>BlockId</c> anchors it to a block; <c>ParentCommentId</c> makes it a reply.</summary>
public record CreateCommentRequest(
    [Required][MaxLength(4000)] string? Body,
    Guid? BlockId,
    Guid? ParentCommentId);
