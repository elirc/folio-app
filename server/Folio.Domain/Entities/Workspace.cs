namespace Folio.Domain.Entities;

/// <summary>A top-level container owning members and a tree of pages.</summary>
public class Workspace
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;

    /// <summary>UTC. Stored as DateTime because SQLite cannot order/compare DateTimeOffset.</summary>
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<Member> Members { get; set; } = new List<Member>();
    public ICollection<Page> Pages { get; set; } = new List<Page>();
}
