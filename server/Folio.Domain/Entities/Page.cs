namespace Folio.Domain.Entities;

/// <summary>
/// A document node in a workspace's page tree. Ordering among siblings is by
/// <see cref="Position"/>; the tree is formed by <see cref="ParentId"/>.
/// Sharing, favorites and soft-delete columns are added in later sprints.
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

    /// <summary>Zero-based order among siblings sharing the same parent.</summary>
    public int Position { get; set; }

    public ICollection<Block> Blocks { get; set; } = new List<Block>();

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
