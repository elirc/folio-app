using Folio.Api.Contracts;
using Folio.Api.Services;
using Folio.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Folio.Api.Controllers;

[ApiController]
[Route("api/workspaces")]
public class WorkspacesController(FolioDbContext db, PageService pages) : ControllerBase
{
    /// <summary>Lists workspaces with member and page counts.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkspaceSummaryResponse>>> List(CancellationToken ct)
    {
        var workspaces = await db.Workspaces
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

    /// <summary>Gets a single workspace summary.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<WorkspaceSummaryResponse>> Get(Guid id, CancellationToken ct)
    {
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
