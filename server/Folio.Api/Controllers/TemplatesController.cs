using Folio.Api.Contracts;
using Folio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Folio.Api.Controllers;

[ApiController]
[Authorize]
public class TemplatesController(TemplateService templates) : ControllerBase
{
    /// <summary>Create a template from a page.</summary>
    [HttpPost("/api/pages/{pageId:guid}/templates")]
    public async Task<ActionResult<TemplateResponse>> CreateFromPage(Guid pageId, [FromBody] CreateTemplateRequest request, CancellationToken ct)
    {
        var result = await templates.CreateFromPageAsync(pageId, request, ct);
        return result.Status switch
        {
            OperationStatus.Success => Created($"/api/workspaces/{result.Value!.WorkspaceId}/templates", result.Value),
            OperationStatus.NotFound => Problem(statusCode: 404, detail: result.Error),
            OperationStatus.Forbidden => Problem(statusCode: 403, detail: result.Error),
            _ => Problem(statusCode: 400, detail: result.Error),
        };
    }

    /// <summary>Templates in the caller's workspace.</summary>
    [HttpGet("/api/workspaces/{workspaceId:guid}/templates")]
    public async Task<ActionResult<IReadOnlyList<TemplateResponse>>> List(Guid workspaceId, CancellationToken ct) =>
        Map(await templates.ListAsync(workspaceId, ct));

    /// <summary>Create a new page from a template.</summary>
    [HttpPost("/api/workspaces/{workspaceId:guid}/templates/{templateId:guid}/instantiate")]
    public async Task<ActionResult<PageDetailResponse>> Instantiate(Guid workspaceId, Guid templateId, [FromBody] InstantiateTemplateRequest request, CancellationToken ct)
    {
        var result = await templates.InstantiateAsync(workspaceId, templateId, request, ct);
        return result.Status switch
        {
            OperationStatus.Success => Created($"/api/pages/{result.Value!.Id}", result.Value),
            OperationStatus.NotFound => Problem(statusCode: 404, detail: result.Error),
            OperationStatus.Forbidden => Problem(statusCode: 403, detail: result.Error),
            _ => Problem(statusCode: 400, detail: result.Error),
        };
    }

    /// <summary>Delete a template.</summary>
    [HttpDelete("/api/workspaces/{workspaceId:guid}/templates/{templateId:guid}")]
    public async Task<IActionResult> Delete(Guid workspaceId, Guid templateId, CancellationToken ct)
    {
        var result = await templates.DeleteAsync(workspaceId, templateId, ct);
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
