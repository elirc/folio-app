using Folio.Domain.Enums;

namespace Folio.Domain.Entities;

/// <summary>
/// A typed content block on a page. <see cref="Content"/> holds a
/// type-specific JSON payload (kept as text so SQLite stores it verbatim).
/// </summary>
public class Block
{
    public Guid Id { get; set; }

    public Guid PageId { get; set; }
    public Page? Page { get; set; }

    /// <summary>Parent block for nesting (children live under a Toggle). Null = top level.</summary>
    public Guid? ParentBlockId { get; set; }
    public Block? Parent { get; set; }
    public ICollection<Block> Children { get; set; } = new List<Block>();

    public BlockType Type { get; set; }

    /// <summary>Zero-based order among siblings sharing the same parent (or page root).</summary>
    public int Position { get; set; }

    /// <summary>Type-specific payload as JSON, e.g. {"text":"…","level":1}.</summary>
    public string Content { get; set; } = "{}";

    /// <summary>Optimistic-concurrency token; changes on every write, checked on stale updates.</summary>
    public Guid Version { get; set; } = Guid.NewGuid();

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
