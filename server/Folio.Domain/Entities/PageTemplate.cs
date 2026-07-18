namespace Folio.Domain.Entities;

/// <summary>
/// A reusable page template: a name/description plus a captured title, icon, and
/// block subtree (stored as JSON). Instantiating one creates a fresh page.
/// </summary>
public class PageTemplate
{
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }
    public Workspace? Workspace { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>The captured page title/icon the template reproduces.</summary>
    public string SourceTitle { get; set; } = string.Empty;
    public string? SourceIcon { get; set; }

    /// <summary>Serialized block set (a JSON array of block snapshots).</summary>
    public string BlocksJson { get; set; } = "[]";
    public int BlockCount { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public string? CreatedByName { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }
}
