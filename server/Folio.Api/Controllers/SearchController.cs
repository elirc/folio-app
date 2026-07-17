using Folio.Api.Contracts;
using Folio.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Folio.Api.Controllers;

[ApiController]
public class SearchController(SearchService search) : ControllerBase
{
    /// <summary>Full-text search over page titles and block text in a workspace.</summary>
    [HttpGet("/api/workspaces/{workspaceId:guid}/search")]
    public async Task<ActionResult<IReadOnlyList<SearchResultResponse>>> Search(
        Guid workspaceId,
        [FromQuery] string? q,
        CancellationToken ct)
    {
        var results = await search.SearchAsync(workspaceId, q, ct);
        return results is null ? Problem(statusCode: 404, detail: "Workspace not found.") : Ok(results);
    }
}
