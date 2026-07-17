namespace Folio.Domain.Entities;

/// <summary>
/// An immutable snapshot of a page (its title/icon and full block set) captured
/// on save. Versions are append-only history — restoring one never mutates or
/// deletes existing versions.
/// </summary>
public class PageVersion
{
    public Guid Id { get; set; }

    public Guid PageId { get; set; }
    public Page? Page { get; set; }

    /// <summary>Per-page incrementing sequence number (1, 2, 3, …).</summary>
    public int VersionNumber { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Icon { get; set; }

    /// <summary>Serialized block set at snapshot time (a JSON array of block snapshots).</summary>
    public string BlocksJson { get; set; } = "[]";

    /// <summary>Cached block count for cheap list rendering.</summary>
    public int BlockCount { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public string? CreatedByName { get; set; }

    /// <summary>Optional note, e.g. "Restored from version 3".</summary>
    public string? Label { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }
}
