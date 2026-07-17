using Folio.Api.Contracts;
using Folio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Folio.Api.Controllers;

[ApiController]
[Authorize]
public class BlocksController(BlockService blocks) : ControllerBase
{
    /// <summary>All blocks on a page, in order.</summary>
    [HttpGet("/api/pages/{pageId:guid}/blocks")]
    public async Task<ActionResult<IReadOnlyList<BlockResponse>>> List(Guid pageId, CancellationToken ct)
    {
        var result = await blocks.GetForPageAsync(pageId, ct);
        return result.Status switch
        {
            OperationStatus.Success => Ok(result.Value),
            OperationStatus.NotFound => Problem(statusCode: 404, detail: result.Error),
            OperationStatus.Forbidden => Problem(statusCode: 403, detail: result.Error),
            _ => Problem(statusCode: 400, detail: result.Error),
        };
    }

    /// <summary>Create a block (appended unless a position is given).</summary>
    [HttpPost("/api/pages/{pageId:guid}/blocks")]
    public async Task<ActionResult<BlockResponse>> Create(Guid pageId, [FromBody] CreateBlockRequest request, CancellationToken ct)
    {
        var result = await blocks.CreateAsync(pageId, request, ct);
        return result.Status switch
        {
            OperationStatus.Success => Created($"/api/blocks/{result.Value!.Id}", result.Value),
            OperationStatus.NotFound => Problem(statusCode: 404, detail: result.Error),
            OperationStatus.Forbidden => Problem(statusCode: 403, detail: result.Error),
            _ => Problem(statusCode: 400, detail: result.Error),
        };
    }

    /// <summary>Update a block's content and/or type.</summary>
    [HttpPut("/api/blocks/{id:guid}")]
    public async Task<ActionResult<BlockResponse>> Update(Guid id, [FromBody] UpdateBlockRequest request, CancellationToken ct)
    {
        var result = await blocks.UpdateAsync(id, request, ct);
        return Map(result);
    }

    /// <summary>Reorder a block within its page.</summary>
    [HttpPost("/api/blocks/{id:guid}/move")]
    public async Task<ActionResult<BlockResponse>> Move(Guid id, [FromBody] MoveBlockRequest request, CancellationToken ct)
    {
        var result = await blocks.MoveAsync(id, request, ct);
        return Map(result);
    }

    /// <summary>Delete a block.</summary>
    [HttpDelete("/api/blocks/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await blocks.DeleteAsync(id, ct);
        return result.Status switch
        {
            OperationStatus.Success => NoContent(),
            OperationStatus.NotFound => Problem(statusCode: 404, detail: result.Error),
            OperationStatus.Forbidden => Problem(statusCode: 403, detail: result.Error),
            _ => Problem(statusCode: 400, detail: result.Error),
        };
    }

    private ActionResult<BlockResponse> Map(ServiceResult<BlockResponse> result) =>
        result.Status switch
        {
            OperationStatus.Success => Ok(result.Value),
            OperationStatus.NotFound => Problem(statusCode: 404, detail: result.Error),
            OperationStatus.Forbidden => Problem(statusCode: 403, detail: result.Error),
            _ => Problem(statusCode: 400, detail: result.Error),
        };
}
