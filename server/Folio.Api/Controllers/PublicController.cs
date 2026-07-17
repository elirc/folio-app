using Folio.Api.Contracts;
using Folio.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Folio.Api.Controllers;

[ApiController]
public class PublicController(PageService pages) : ControllerBase
{
    /// <summary>Resolve a page shared via public link. 404 unless still Public.</summary>
    [HttpGet("/api/public/pages/{slug}")]
    public async Task<ActionResult<PageDetailResponse>> Get(string slug, CancellationToken ct)
    {
        var page = await pages.GetPublicBySlugAsync(slug, ct);
        return page is null ? Problem(statusCode: 404, detail: "No public page for that link.") : Ok(page);
    }
}
