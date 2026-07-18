using Folio.Api.Contracts;
using Folio.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Folio.Api.Controllers;

[ApiController]
[Authorize]
public class SearchController(SearchService search) : ControllerBase
{
    /// <summary>Full-text search over page titles and block text, with optional filters.</summary>
    [HttpGet("/api/workspaces/{workspaceId:guid}/search")]
    public async Task<ActionResult<IReadOnlyList<SearchResultResponse>>> Search(
        Guid workspaceId,
        [FromQuery] string? q,
        [FromQuery] Guid? author,
        [FromQuery] bool? favorites,
        [FromQuery] DateTime? updatedAfter,
        [FromQuery] DateTime? updatedBefore,
        CancellationToken ct)
    {
        var filters = new SearchFilters(author, favorites, updatedAfter, updatedBefore);
        var results = await search.SearchAsync(workspaceId, q, filters, ct);
        return results is null ? Problem(statusCode: 404, detail: "Workspace not found.") : Ok(results);
    }

    /// <summary>Quick-open: title-prefix ranked matches, or recent pages when the query is empty.</summary>
    [HttpGet("/api/workspaces/{workspaceId:guid}/quick-open")]
    public async Task<ActionResult<IReadOnlyList<QuickOpenResult>>> QuickOpen(
        Guid workspaceId,
        [FromQuery] string? q,
        CancellationToken ct)
    {
        var results = await search.QuickOpenAsync(workspaceId, q, ct);
        return results is null ? Problem(statusCode: 404, detail: "Workspace not found.") : Ok(results);
    }
}
