using Folio.Api.Contracts;
using Folio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Folio.Api.Controllers;

[ApiController]
[Authorize]
public class VersionsController(PageVersionService versions) : ControllerBase
{
    /// <summary>Save the page's current state as a new version.</summary>
    [HttpPost("/api/pages/{pageId:guid}/versions")]
    public async Task<ActionResult<VersionSummaryResponse>> Create(Guid pageId, CancellationToken ct)
    {
        var result = await versions.SnapshotAsync(pageId, ct);
        return result.Status switch
        {
            OperationStatus.Success => Created($"/api/pages/{pageId}/versions/{result.Value!.VersionNumber}", result.Value),
            OperationStatus.NotFound => Problem(statusCode: 404, detail: result.Error),
            OperationStatus.Forbidden => Problem(statusCode: 403, detail: result.Error),
            _ => Problem(statusCode: 400, detail: result.Error),
        };
    }

    /// <summary>List the page's versions, newest first.</summary>
    [HttpGet("/api/pages/{pageId:guid}/versions")]
    public async Task<ActionResult<IReadOnlyList<VersionSummaryResponse>>> List(Guid pageId, CancellationToken ct) =>
        Map(await versions.ListAsync(pageId, ct));

    /// <summary>A version's full snapshot plus a diff against the current page.</summary>
    [HttpGet("/api/pages/{pageId:guid}/versions/{versionNumber:int}")]
    public async Task<ActionResult<VersionDetailResponse>> Get(Guid pageId, int versionNumber, CancellationToken ct) =>
        Map(await versions.GetAsync(pageId, versionNumber, ct));

    /// <summary>Restore a version (non-destructive: the current state is snapshotted first).</summary>
    [HttpPost("/api/pages/{pageId:guid}/versions/{versionNumber:int}/restore")]
    public async Task<ActionResult<VersionSummaryResponse>> Restore(Guid pageId, int versionNumber, CancellationToken ct) =>
        Map(await versions.RestoreAsync(pageId, versionNumber, ct));

    private ActionResult<T> Map<T>(ServiceResult<T> result) =>
        result.Status switch
        {
            OperationStatus.Success => Ok(result.Value),
            OperationStatus.NotFound => Problem(statusCode: 404, detail: result.Error),
            OperationStatus.Forbidden => Problem(statusCode: 403, detail: result.Error),
            _ => Problem(statusCode: 400, detail: result.Error),
        };
}
