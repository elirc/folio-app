namespace Folio.Domain.Enums;

/// <summary>Who can reach a page.</summary>
public enum PageVisibility
{
    /// <summary>Only reachable directly by its owner/workspace admins.</summary>
    Private = 0,

    /// <summary>Everyone in the workspace.</summary>
    Workspace = 1,

    /// <summary>Anyone with the public link.</summary>
    Public = 2,
}
