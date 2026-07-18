using Folio.Domain.Enums;

namespace Folio.Domain.Entities;

/// <summary>
/// A document node in a workspace's page tree. Ordering among siblings is by
/// <see cref="Position"/>; the tree is formed by <see cref="ParentId"/>.
/// </summary>
public class Page
{
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }
    public Workspace? Workspace { get; set; }

    public Guid? ParentId { get; set; }
    public Page? Parent { get; set; }
    public ICollection<Page> Children { get; set; } = new List<Page>();

    public string Title { get; set; } = string.Empty;
    public string? Icon { get; set; }

    /// <summary>The member who created the page (used for authored-page notifications).</summary>
    public Guid? CreatedByMemberId { get; set; }

    /// <summary>Zero-based order among siblings sharing the same parent.</summary>
    public int Position { get; set; }

    public ICollection<Block> Blocks { get; set; } = new List<Block>();

    // ---- sharing / permissions ----
    public PageVisibility Visibility { get; set; } = PageVisibility.Workspace;
    public SharePermission Permission { get; set; } = SharePermission.View;

    /// <summary>Random slug used for public-link access; set only when Public.</summary>
    public string? PublicSlug { get; set; }

    // ---- favorites ----
    public bool IsFavorite { get; set; }

    // ---- trash / soft delete ----
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
