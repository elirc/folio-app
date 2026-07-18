using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Folio.Domain.Enums;

namespace Folio.Api.Services;

/// <summary>Renders a page's block tree to Markdown.</summary>
public static partial class MarkdownRenderer
{
    /// <summary>Minimal block shape needed for rendering.</summary>
    public record MdBlock(Guid Id, Guid? ParentBlockId, BlockType Type, int Position, string Content);

    // Convert inline page links #[Title](guid) into plain [Title] text.
    [GeneratedRegex(@"#\[([^\]]+)\]\([0-9a-fA-F-]{36}\)")]
    private static partial Regex LinkTokenRegex();

    public static string RenderBlocks(IReadOnlyList<MdBlock> blocks)
    {
        var byParent = blocks.OrderBy(b => b.Position).ToLookup(b => b.ParentBlockId);
        var sb = new StringBuilder();

        void Render(Guid? parentId, int indent)
        {
            foreach (var block in byParent[parentId])
            {
                var markdown = RenderBlock(block);
                var prefix = new string(' ', indent * 2);
                foreach (var line in markdown.Split('\n'))
                {
                    sb.Append(prefix).Append(line).Append('\n');
                }
                sb.Append('\n');

                if (block.Type == BlockType.Toggle)
                {
                    Render(block.Id, indent + 1);
                }
            }
        }

        Render(null, 0);
        return sb.ToString().TrimEnd();
    }

    private static string RenderBlock(MdBlock block)
    {
        var json = Parse(block.Content);
        var text = CleanText(Text(json));

        return block.Type switch
        {
            BlockType.Heading => new string('#', Math.Clamp(Level(json), 1, 6)) + " " + text,
            BlockType.Paragraph => text,
            BlockType.Todo => (Bool(json, "checked") ? "- [x] " : "- [ ] ") + text,
            BlockType.Bulleted => "- " + text,
            BlockType.Quote => "> " + text,
            BlockType.Code => Fence(text, Str(json, "language")),
            BlockType.Toggle => "- **" + text + "**",
            BlockType.Callout => "> " + (Str(json, "emoji") is { Length: > 0 } e ? e + " " : "") + text,
            BlockType.Divider => "---",
            BlockType.Image => $"![{CleanText(Str(json, "alt") ?? "")}]({Str(json, "url") ?? ""})",
            BlockType.Table => RenderTable(json),
            _ => text,
        };
    }

    private static string Fence(string text, string? language) =>
        "```" + (language ?? "") + "\n" + text + "\n```";

    private static string RenderTable(JsonElement json)
    {
        if (!json.TryGetProperty("rows", out var rowsEl) || rowsEl.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var rows = rowsEl.EnumerateArray()
            .Select(r => r.EnumerateArray().Select(c => (c.GetString() ?? "").Replace("|", "\\|")).ToList())
            .ToList();
        if (rows.Count == 0)
        {
            return string.Empty;
        }

        var width = rows.Max(r => r.Count);
        var sb = new StringBuilder();
        void WriteRow(List<string> cells) =>
            sb.Append("| ").Append(string.Join(" | ", Enumerable.Range(0, width).Select(i => i < cells.Count ? cells[i] : ""))).Append(" |\n");

        WriteRow(rows[0]);
        sb.Append("| ").Append(string.Join(" | ", Enumerable.Repeat("---", width))).Append(" |\n");
        foreach (var row in rows.Skip(1))
        {
            WriteRow(row);
        }

        return sb.ToString().TrimEnd('\n');
    }

    // ---- json helpers ----

    private static JsonElement Parse(string content)
    {
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(content);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static string Text(JsonElement json) => Str(json, "text") ?? "";
    private static int Level(JsonElement json) => json.ValueKind == JsonValueKind.Object
        && json.TryGetProperty("level", out var l) && l.ValueKind == JsonValueKind.Number ? l.GetInt32() : 1;

    private static string? Str(JsonElement json, string name) =>
        json.ValueKind == JsonValueKind.Object && json.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static bool Bool(JsonElement json, string name) =>
        json.ValueKind == JsonValueKind.Object && json.TryGetProperty(name, out var v)
        && (v.ValueKind == JsonValueKind.True || (v.ValueKind == JsonValueKind.False && false));

    private static string CleanText(string text) => LinkTokenRegex().Replace(text, "[$1]");
}
