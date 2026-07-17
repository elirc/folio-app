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

    public BlockType Type { get; set; }

    /// <summary>Zero-based order within the page.</summary>
    public int Position { get; set; }

    /// <summary>Type-specific payload as JSON, e.g. {"text":"…","level":1}.</summary>
    public string Content { get; set; } = "{}";

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
