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
}
