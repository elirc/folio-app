using Folio.Domain.Enums;

namespace Folio.Api.Auth;

/// <summary>Outcome of an access check, mapped by callers to 200/403/404.</summary>
public enum AccessResult
{
    Allowed,

    /// <summary>The resource is in another workspace — reported as 404 to avoid leaking existence.</summary>
    NotFound,

    /// <summary>Visible workspace, but the member's role/permission is insufficient — 403.</summary>
    Forbidden,
}

/// <summary>
/// Central page-permission rules shared by every service that touches pages or
/// blocks. Reads and writes are evaluated against the caller's workspace + role
/// and the page's visibility + share permission.
/// </summary>
public static class PageAuthorization
{
    /// <summary>Can the member see this page at all?</summary>
    public static AccessResult CanRead(CurrentMember? member, Guid pageWorkspaceId, PageVisibility visibility)
    {
        if (member is null || member.WorkspaceId != pageWorkspaceId)
        {
            // Anonymous callers never reach authorized endpoints; a mismatched
            // workspace is a 404 so foreign pages are indistinguishable from missing ones.
            return AccessResult.NotFound;
        }

        // Private pages are for workspace admins (Owners) only.
        if (visibility == PageVisibility.Private && member.Role != MemberRole.Owner)
        {
            return AccessResult.Forbidden;
        }

        return AccessResult.Allowed;
    }

    /// <summary>Can the member modify this page (or its blocks)?</summary>
    public static AccessResult CanWrite(
        CurrentMember? member,
        Guid pageWorkspaceId,
        PageVisibility visibility,
        SharePermission permission)
    {
        var read = CanRead(member, pageWorkspaceId, visibility);
        if (read != AccessResult.Allowed)
        {
            return read;
        }

        // Owners can always write; Viewers never can; Editors need Edit permission.
        return member!.Role switch
        {
            MemberRole.Owner => AccessResult.Allowed,
            MemberRole.Viewer => AccessResult.Forbidden,
            _ => permission == SharePermission.Edit ? AccessResult.Allowed : AccessResult.Forbidden,
        };
    }

    /// <summary>Whether the member may see pages of this visibility (used to filter list/tree results).</summary>
    public static bool CanSeeVisibility(CurrentMember member, PageVisibility visibility) =>
        visibility != PageVisibility.Private || member.Role == MemberRole.Owner;
}
