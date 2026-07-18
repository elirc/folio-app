namespace Folio.Domain.Entities;

/// <summary>
/// A materialized inline reference from a block to another page. Kept in sync
/// whenever the source block's content changes and removed when the block is
/// deleted (cascade). The target may be missing/trashed — that's a "broken" link,
/// determined at query time rather than stored.
/// </summary>
public class PageLink
{
    public Guid Id { get; set; }

    /// <summary>The page that contains the linking block.</summary>
    public Guid SourcePageId { get; set; }

    public Guid SourceBlockId { get; set; }
    public Block? SourceBlock { get; set; }

    /// <summary>The linked-to page id (no FK — may point at a trashed/removed page).</summary>
    public Guid TargetPageId { get; set; }

    /// <summary>Display title captured from the link token, for showing broken links.</summary>
    public string TargetTitle { get; set; } = string.Empty;

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }
}
