using Folio.Api.Contracts;
using Folio.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Folio.Api.Controllers;

[ApiController]
public class PagesController(PageService pages) : ControllerBase
{
    /// <summary>Nested page tree for a workspace's sidebar.</summary>
    [HttpGet("/api/workspaces/{workspaceId:guid}/pages/tree")]
    public async Task<ActionResult<IReadOnlyList<PageTreeNode>>> Tree(Guid workspaceId, CancellationToken ct)
    {
        var tree = await pages.GetTreeAsync(workspaceId, ct);
        return tree is null ? Problem(statusCode: 404, detail: "Workspace not found.") : Ok(tree);
    }

    /// <summary>Create a page (root or nested).</summary>
    [HttpPost("/api/workspaces/{workspaceId:guid}/pages")]
    public async Task<ActionResult<PageDetailResponse>> Create(Guid workspaceId, [FromBody] CreatePageRequest request, CancellationToken ct)
    {
        var result = await pages.CreateAsync(workspaceId, request, ct);
        return result.Status switch
        {
            OperationStatus.Success => Created($"/api/pages/{result.Value!.Id}", result.Value),
            OperationStatus.NotFound => Problem(statusCode: 404, detail: result.Error),
            _ => Problem(statusCode: 400, detail: result.Error),
        };
    }

    /// <summary>Page detail including breadcrumb.</summary>
    [HttpGet("/api/pages/{id:guid}")]
    public async Task<ActionResult<PageDetailResponse>> Get(Guid id, CancellationToken ct)
    {
        var detail = await pages.GetDetailAsync(id, ct);
        return detail is null ? Problem(statusCode: 404, detail: "Page not found.") : Ok(detail);
    }

    /// <summary>Breadcrumb trail (root to self).</summary>
    [HttpGet("/api/pages/{id:guid}/breadcrumb")]
    public async Task<ActionResult<IReadOnlyList<BreadcrumbItem>>> Breadcrumb(Guid id, CancellationToken ct)
    {
        var trail = await pages.GetBreadcrumbAsync(id, ct);
        return trail is null ? Problem(statusCode: 404, detail: "Page not found.") : Ok(trail);
    }

    /// <summary>Rename a page and/or set its icon.</summary>
    [HttpPut("/api/pages/{id:guid}")]
    public async Task<ActionResult<PageDetailResponse>> Update(Guid id, [FromBody] UpdatePageRequest request, CancellationToken ct)
    {
        var result = await pages.UpdateAsync(id, request, ct);
        return MapToDetail(result);
    }

    /// <summary>Move/reorder a page under a new parent at a position.</summary>
    [HttpPost("/api/pages/{id:guid}/move")]
    public async Task<ActionResult<PageDetailResponse>> Move(Guid id, [FromBody] MovePageRequest request, CancellationToken ct)
    {
        var result = await pages.MoveAsync(id, request, ct);
        return MapToDetail(result);
    }

    /// <summary>Delete a page and its subtree.</summary>
    [HttpDelete("/api/pages/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await pages.DeleteAsync(id, ct);
        return result.Status switch
        {
            OperationStatus.Success => NoContent(),
            OperationStatus.NotFound => Problem(statusCode: 404, detail: result.Error),
            _ => Problem(statusCode: 400, detail: result.Error),
        };
    }

    private ActionResult<PageDetailResponse> MapToDetail(ServiceResult<PageDetailResponse> result) =>
        result.Status switch
        {
            OperationStatus.Success => Ok(result.Value),
            OperationStatus.NotFound => Problem(statusCode: 404, detail: result.Error),
            _ => Problem(statusCode: 400, detail: result.Error),
        };
}
