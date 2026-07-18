using Folio.Api.Auth;
using Folio.Api.Contracts;
using Folio.Api.Services;
using Folio.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Folio.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/workspaces")]
public class WorkspacesController(FolioDbContext db, PageService pages, ICurrentMemberAccessor current) : ControllerBase
{
    /// <summary>Lists the workspaces the caller belongs to (with member and page counts).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkspaceSummaryResponse>>> List(CancellationToken ct)
    {
        var member = current.Member;
        if (member is null)
        {
            return Ok(Array.Empty<WorkspaceSummaryResponse>());
        }

        var workspaces = await db.Workspaces
            .Where(w => w.Id == member.WorkspaceId)
            .OrderBy(w => w.Name)
            .Select(w => new WorkspaceSummaryResponse(
                w.Id,
                w.Name,
                w.Slug,
                w.Members.Count,
                w.Pages.Count,
                w.CreatedAt))
            .ToListAsync(ct);

        return Ok(workspaces);
    }

    /// <summary>Gets a single workspace summary (only the caller's own workspace).</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkspaceSummaryResponse>> Get(Guid id, CancellationToken ct)
    {
        // Foreign workspaces are 404 so their existence isn't leaked.
        if (current.Member?.WorkspaceId != id)
        {
            return NotFound();
        }

        var workspace = await db.Workspaces
            .Where(w => w.Id == id)
            .Select(w => new WorkspaceSummaryResponse(
                w.Id,
                w.Name,
                w.Slug,
                w.Members.Count,
                w.Pages.Count,
                w.CreatedAt))
            .FirstOrDefaultAsync(ct);

        return workspace is null ? NotFound() : Ok(workspace);
    }

    /// <summary>Recent activity feed for the caller's workspace.</summary>
    [HttpGet("{workspaceId:guid}/activity")]
    public async Task<ActionResult<IReadOnlyList<ActivityResponse>>> Activity(
        Guid workspaceId,
        [FromQuery] int limit,
        [FromServices] NotificationService notifications,
        CancellationToken ct)
    {
        var feed = await notifications.GetWorkspaceActivityAsync(workspaceId, limit == 0 ? 50 : limit, ct);
        return feed is null ? Problem(statusCode: 404, detail: "Workspace not found.") : Ok(feed);
    }

    /// <summary>Members of the caller's workspace (used by the @mention picker).</summary>
    [HttpGet("{workspaceId:guid}/members")]
    public async Task<ActionResult<IReadOnlyList<MemberResponse>>> Members(Guid workspaceId, CancellationToken ct)
    {
        if (current.Member?.WorkspaceId != workspaceId)
        {
            return NotFound();
        }

        var members = await db.Members
            .Where(m => m.WorkspaceId == workspaceId)
            .OrderBy(m => m.Name)
            .Select(m => new MemberResponse(m.Id, m.WorkspaceId, m.Name, m.Email, m.Role))
            .ToListAsync(ct);

        return Ok(members);
    }

    /// <summary>Pages currently in the workspace's trash (roots of trashed subtrees).</summary>
    [HttpGet("{workspaceId:guid}/trash")]
    public async Task<ActionResult<IReadOnlyList<TrashItemResponse>>> Trash(Guid workspaceId, CancellationToken ct)
    {
        var trash = await pages.GetTrashAsync(workspaceId, ct);
        return trash is null ? Problem(statusCode: 404, detail: "Workspace not found.") : Ok(trash);
    }

    /// <summary>Favorite pages in the workspace.</summary>
    [HttpGet("{workspaceId:guid}/favorites")]
    public async Task<ActionResult<IReadOnlyList<FavoriteResponse>>> Favorites(Guid workspaceId, CancellationToken ct)
    {
        var favorites = await pages.GetFavoritesAsync(workspaceId, ct);
        return favorites is null ? Problem(statusCode: 404, detail: "Workspace not found.") : Ok(favorites);
    }

    /// <summary>Paginated list of a workspace's pages, most-recently-updated first.</summary>
    [HttpGet("{workspaceId:guid}/pages")]
    public async Task<ActionResult<PagedResponse<PageListItemResponse>>> RecentPages(
        Guid workspaceId,
        [FromQuery] PaginationQuery pagination,
        CancellationToken ct)
    {
        var result = await pages.GetRecentAsync(workspaceId, pagination.Page, pagination.PageSize, ct);
        return result is null ? Problem(statusCode: 404, detail: "Workspace not found.") : Ok(result);
    }
}
