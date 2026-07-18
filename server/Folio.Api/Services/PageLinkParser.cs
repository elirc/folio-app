using System.Text.RegularExpressions;

namespace Folio.Api.Services;

/// <summary>
/// Extracts inline page-link tokens from raw block content. A link is written as
/// <c>#[Display Title](page-guid)</c> — analogous to the @[…](id) mention token.
/// </summary>
public static partial class PageLinkParser
{
    [GeneratedRegex(@"#\[(?<title>[^\]]+)\]\((?<id>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\)")]
    private static partial Regex LinkRegex();

    public static IEnumerable<(Guid TargetId, string Title)> Extract(string content)
    {
        foreach (Match match in LinkRegex().Matches(content))
        {
            if (Guid.TryParse(match.Groups["id"].Value, out var id))
            {
                yield return (id, match.Groups["title"].Value);
            }
        }
    }
}
