using Folio.Api.Contracts;
using Folio.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Folio.Api.Controllers;

[ApiController]
[Route("api/workspaces")]
public class WorkspacesController(FolioDbContext db) : ControllerBase
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
}
