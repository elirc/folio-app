using Folio.Api.Contracts;
using Folio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Folio.Api.Controllers;

[ApiController]
[Authorize]
public class LinksController(LinkService links) : ControllerBase
{
    /// <summary>Pages that link to this page.</summary>
    [HttpGet("/api/pages/{pageId:guid}/backlinks")]
    public async Task<ActionResult<IReadOnlyList<BacklinkResponse>>> Backlinks(Guid pageId, CancellationToken ct) =>
        Map(await links.GetBacklinksAsync(pageId, ct));

    /// <summary>Links leaving this page (broken ones flagged).</summary>
    [HttpGet("/api/pages/{pageId:guid}/links")]
    public async Task<ActionResult<IReadOnlyList<OutgoingLinkResponse>>> Outgoing(Guid pageId, CancellationToken ct) =>
        Map(await links.GetOutgoingAsync(pageId, ct));

    private ActionResult<T> Map<T>(ServiceResult<T> result) =>
        result.Status switch
        {
            OperationStatus.Success => Ok(result.Value),
            OperationStatus.NotFound => Problem(statusCode: 404, detail: result.Error),
            OperationStatus.Forbidden => Problem(statusCode: 403, detail: result.Error),
            _ => Problem(statusCode: 400, detail: result.Error),
        };
}
