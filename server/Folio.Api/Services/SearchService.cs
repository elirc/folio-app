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
    /// <summary>Returns hits (respecting page visibility), or null if the workspace is unknown/foreign.</summary>
    public async Task<IReadOnlyList<SearchResultResponse>?> SearchAsync(Guid workspaceId, string? query, CancellationToken ct)
    {
        var member = current.Member;
        if (member is null || member.WorkspaceId != workspaceId
            || !await db.Workspaces.AnyAsync(w => w.Id == workspaceId, ct))
        {
            return null;
        }

        var term = query?.Trim() ?? string.Empty;
        if (term.Length == 0)
        {
            return [];
        }

        var pattern = $"%{Escape(term)}%";
        var isOwner = member.Role == MemberRole.Owner;

        var hits = await db.Pages
            .Where(p => p.WorkspaceId == workspaceId
                // Non-owners never see private pages in results.
                && (isOwner || p.Visibility != PageVisibility.Private)
                && (EF.Functions.Like(p.Title, pattern, "\\")
                    || p.Blocks.Any(b => EF.Functions.Like(b.Content, pattern, "\\"))))
            .OrderBy(p => p.Title)
            .Select(p => new
            {
                p.Id,
                p.Title,
                p.Icon,
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
                h.TitleMatch ? null : SnippetFrom(h.BlockContent, term)))
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
