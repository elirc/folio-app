using Folio.Api.Contracts;
using Folio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Folio.Api.Controllers;

[ApiController]
[Authorize]
public class CommentsController(CommentService comments) : ControllerBase
{
    /// <summary>All comments on a page (page-level and block-level).</summary>
    [HttpGet("/api/pages/{pageId:guid}/comments")]
    public async Task<ActionResult<IReadOnlyList<CommentResponse>>> List(Guid pageId, CancellationToken ct) =>
        Map(await comments.GetForPageAsync(pageId, ct));

    /// <summary>Create a comment (page-level, block-level, or a reply).</summary>
    [HttpPost("/api/pages/{pageId:guid}/comments")]
    public async Task<ActionResult<CommentResponse>> Create(Guid pageId, [FromBody] CreateCommentRequest request, CancellationToken ct)
    {
        var result = await comments.CreateAsync(pageId, request, ct);
        return result.Status switch
        {
            OperationStatus.Success => Created($"/api/comments/{result.Value!.Id}", result.Value),
            OperationStatus.NotFound => Problem(statusCode: 404, detail: result.Error),
            OperationStatus.Forbidden => Problem(statusCode: 403, detail: result.Error),
            _ => Problem(statusCode: 400, detail: result.Error),
        };
    }

    /// <summary>Resolve a comment thread.</summary>
    [HttpPost("/api/comments/{id:guid}/resolve")]
    public async Task<ActionResult<CommentResponse>> Resolve(Guid id, CancellationToken ct) =>
        Map(await comments.SetResolvedAsync(id, true, ct));

    /// <summary>Reopen a resolved comment thread.</summary>
    [HttpPost("/api/comments/{id:guid}/unresolve")]
    public async Task<ActionResult<CommentResponse>> Unresolve(Guid id, CancellationToken ct) =>
        Map(await comments.SetResolvedAsync(id, false, ct));

    /// <summary>Delete a comment (and its replies).</summary>
    [HttpDelete("/api/comments/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await comments.DeleteAsync(id, ct);
        return result.Status switch
        {
            OperationStatus.Success => NoContent(),
            OperationStatus.NotFound => Problem(statusCode: 404, detail: result.Error),
            OperationStatus.Forbidden => Problem(statusCode: 403, detail: result.Error),
            _ => Problem(statusCode: 400, detail: result.Error),
        };
    }

    private ActionResult<T> Map<T>(ServiceResult<T> result) =>
        result.Status switch
        {
            OperationStatus.Success => Ok(result.Value),
            OperationStatus.NotFound => Problem(statusCode: 404, detail: result.Error),
            OperationStatus.Forbidden => Problem(statusCode: 403, detail: result.Error),
            _ => Problem(statusCode: 400, detail: result.Error),
        };
}
