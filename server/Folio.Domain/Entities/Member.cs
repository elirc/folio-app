using Folio.Domain.Enums;

namespace Folio.Domain.Entities;

/// <summary>A person belonging to a workspace, with a role.</summary>
public class Member
{
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }
    public Workspace? Workspace { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public MemberRole Role { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }
}
