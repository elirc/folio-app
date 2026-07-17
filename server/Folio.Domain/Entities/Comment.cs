namespace Folio.Domain.Entities;

/// <summary>
/// A comment on a page (BlockId null) or on a specific block. Replies point at a
/// root comment via <see cref="ParentCommentId"/>; resolving applies to a thread.
/// </summary>
public class Comment
{
    public Guid Id { get; set; }

    public Guid PageId { get; set; }
    public Page? Page { get; set; }

    /// <summary>The block this comment is anchored to; null for a page-level comment.</summary>
    public Guid? BlockId { get; set; }

    /// <summary>Root comment for replies; null for a thread root.</summary>
    public Guid? ParentCommentId { get; set; }
    public Comment? Parent { get; set; }
    public ICollection<Comment> Replies { get; set; } = new List<Comment>();

    public Guid AuthorMemberId { get; set; }
    public string AuthorName { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public bool IsResolved { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public ICollection<CommentMention> Mentions { get; set; } = new List<CommentMention>();

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>A parsed @mention reference from a comment body to a workspace member.</summary>
public class CommentMention
{
    public Guid Id { get; set; }

    public Guid CommentId { get; set; }
    public Comment? Comment { get; set; }

    public Guid MemberId { get; set; }
    public Member? Member { get; set; }
}
