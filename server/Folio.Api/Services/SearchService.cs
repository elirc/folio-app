using System.Text.Json;
using Folio.Api.Auth;
using Folio.Api.Contracts;
using Folio.Domain.Enums;
using Folio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Folio.Api.Services;

/// <summary>Full-text search over page titles and block text within a workspace.</summary>
public class SearchService(FolioDbContext db, ICurrentMemberAccessor current)
{
    /// <summary>Upper bound on rows a single search returns (pagination audit).</summary>
    private const int SearchResultCap = 200;

    /// <summary>Returns hits (respecting page visibility + filters), or null if the workspace is foreign.</summary>
    public async Task<IReadOnlyList<SearchResultResponse>?> SearchAsync(
        Guid workspaceId,
        string? query,
        SearchFilters filters,
        CancellationToken ct)
    {
        var member = current.Member;
        if (member is null || member.WorkspaceId != workspaceId
            || !await db.Workspaces.AnyAsync(w => w.Id == workspaceId, ct))
        {
            return null;
        }

        var term = query?.Trim() ?? string.Empty;
        var hasFilters = filters.Author is not null || filters.Favorites == true
            || filters.UpdatedAfter is not null || filters.UpdatedBefore is not null;
        if (term.Length == 0 && !hasFilters)
        {
            return [];
        }

        var isOwner = member.Role == MemberRole.Owner;
        var query0 = db.Pages.Where(p => p.WorkspaceId == workspaceId
            && (isOwner || p.Visibility != PageVisibility.Private));

        if (filters.Author is Guid author)
        {
            query0 = query0.Where(p => p.CreatedByMemberId == author);
        }
        if (filters.Favorites == true)
        {
            query0 = query0.Where(p => p.IsFavorite);
        }
        if (filters.UpdatedAfter is DateTime after)
        {
            query0 = query0.Where(p => p.UpdatedAt >= after);
        }
        if (filters.UpdatedBefore is DateTime before)
        {
            query0 = query0.Where(p => p.UpdatedAt <= before);
        }

        // Filter-only search (no term): return the filtered pages, most-recent first.
        if (term.Length == 0)
        {
            var filtered = await query0
                .OrderByDescending(p => p.UpdatedAt)
                .Take(SearchResultCap)
                .Select(p => new { p.Id, p.Title, p.Icon, p.UpdatedAt })
                .ToListAsync(ct);
            return filtered
                .Select(p => new SearchResultResponse(p.Id, p.Title, p.Icon, false, null, p.UpdatedAt))
                .ToList();
        }

        var pattern = $"%{Escape(term)}%";
        var hits = await query0
            .Where(p => EF.Functions.Like(p.Title, pattern, "\\")
                || p.Blocks.Any(b => EF.Functions.Like(b.Content, pattern, "\\")))
            .OrderByDescending(p => p.UpdatedAt)
            .Take(SearchResultCap)
            .Select(p => new
            {
                p.Id,
                p.Title,
                p.Icon,
                p.UpdatedAt,
                TitleMatch = EF.Functions.Like(p.Title, pattern, "\\"),
                BlockContent = p.Blocks
                    .Where(b => EF.Functions.Like(b.Content, pattern, "\\"))
                    .OrderBy(b => b.Position)
                    .Select(b => b.Content)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        return hits
            .Select(h => new SearchResultResponse(
                h.Id,
                h.Title,
                h.Icon,
                h.TitleMatch,
                h.TitleMatch ? null : SnippetFrom(h.BlockContent, term),
                h.UpdatedAt))
            .ToList();
    }

    /// <summary>
    /// Quick-open: with a query, title matches ranked prefix-first; without one,
    /// the most recently updated pages. Respects visibility. Foreign → null.
    /// </summary>
    public async Task<IReadOnlyList<QuickOpenResult>?> QuickOpenAsync(Guid workspaceId, string? query, CancellationToken ct)
    {
        var member = current.Member;
        if (member is null || member.WorkspaceId != workspaceId
            || !await db.Workspaces.AnyAsync(w => w.Id == workspaceId, ct))
        {
            return null;
        }

        var isOwner = member.Role == MemberRole.Owner;
        var visible = db.Pages.Where(p => p.WorkspaceId == workspaceId
            && (isOwner || p.Visibility != PageVisibility.Private));

        var term = query?.Trim() ?? string.Empty;
        if (term.Length == 0)
        {
            // Recent pages.
            return await visible
                .OrderByDescending(p => p.UpdatedAt)
                .Take(10)
                .Select(p => new QuickOpenResult(p.Id, p.Title, p.Icon, p.UpdatedAt))
                .ToListAsync(ct);
        }

        var pattern = $"%{Escape(term)}%";
        var matches = await visible
            .Where(p => EF.Functions.Like(p.Title, pattern, "\\"))
            .Select(p => new { p.Id, p.Title, p.Icon, p.UpdatedAt })
            .ToListAsync(ct);

        // Rank: title starting with the term first, then most-recently updated.
        return matches
            .OrderBy(p => p.Title.StartsWith(term, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenByDescending(p => p.UpdatedAt)
            .Take(10)
            .Select(p => new QuickOpenResult(p.Id, p.Title, p.Icon, p.UpdatedAt))
            .ToList();
    }

    private static string? SnippetFrom(string? blockContentJson, string term)
    {
        if (blockContentJson is null)
        {
            return null;
        }

        string text;
        try
        {
            var element = JsonSerializer.Deserialize<JsonElement>(blockContentJson);
            text = element.ValueKind == JsonValueKind.Object && element.TryGetProperty("text", out var t)
                ? t.GetString() ?? string.Empty
                : blockContentJson;
        }
        catch (JsonException)
        {
            text = blockContentJson;
        }

        var index = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return Truncate(text, 140);
        }

        var start = Math.Max(0, index - 40);
        var slice = text.Substring(start, Math.Min(text.Length - start, 140));
        return (start > 0 ? "…" : string.Empty) + slice.Trim();
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max].TrimEnd() + "…";

    // Escape LIKE wildcards so a query with % or _ is matched literally.
    private static string Escape(string term) => term
        .Replace("\\", "\\\\")
        .Replace("%", "\\%")
        .Replace("_", "\\_");
}
