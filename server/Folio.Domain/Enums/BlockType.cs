namespace Folio.Domain.Enums;

/// <summary>
/// The kind of a content block. Each type stores its own JSON payload
/// (see <c>Block.Content</c>) — e.g. a heading carries text + level, a
/// to-do carries text + checked, code carries text + language.
/// </summary>
public enum BlockType
{
    Paragraph = 0,
    Heading = 1,
    Todo = 2,
    Bulleted = 3,
    Quote = 4,
    Code = 5,

    // ---- v2 block types ----
    /// <summary>A grid of cells: {"rows":[["a","b"],["c","d"]]}.</summary>
    Table = 6,

    /// <summary>Collapsible container with child blocks: {"text":"…","collapsed":false}.</summary>
    Toggle = 7,

    /// <summary>Highlighted note: {"text":"…","emoji":"💡"}.</summary>
    Callout = 8,

    /// <summary>Horizontal rule; empty payload {}.</summary>
    Divider = 9,

    /// <summary>External image: {"url":"https://…","alt":"…"}.</summary>
    Image = 10,
}
