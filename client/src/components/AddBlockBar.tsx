import type { BlockContent, BlockType } from "../api/types";

interface AddOption {
  type: BlockType;
  label: string;
  content: BlockContent;
}

/** The palette of block kinds that can be inserted (root level or under a toggle). */
export const ADD_OPTIONS: AddOption[] = [
  { type: "Paragraph", label: "Text", content: { text: "" } },
  { type: "Heading", label: "Heading", content: { text: "", level: 2 } },
  { type: "Todo", label: "To-do", content: { text: "", checked: false } },
  { type: "Bulleted", label: "Bullet", content: { text: "" } },
  { type: "Quote", label: "Quote", content: { text: "" } },
  { type: "Code", label: "Code", content: { text: "", language: "text" } },
  { type: "Table", label: "Table", content: { rows: [["", ""], ["", ""]] } },
  { type: "Toggle", label: "Toggle", content: { text: "", collapsed: false } },
  { type: "Callout", label: "Callout", content: { text: "", emoji: "💡" } },
  { type: "Divider", label: "Divider", content: {} },
  { type: "Image", label: "Image", content: { url: "", alt: "" } },
];

interface AddBlockBarProps {
  onAdd: (type: BlockType, content: BlockContent) => void;
  label?: string;
}

export function AddBlockBar({ onAdd, label = "Add block" }: AddBlockBarProps) {
  return (
    <div className="add-block-bar" role="toolbar" aria-label={label}>
      {ADD_OPTIONS.map((option) => (
        <button
          key={option.type}
          type="button"
          className="add-block-btn"
          onClick={() => onAdd(option.type, option.content)}
        >
          + {option.label}
        </button>
      ))}
    </div>
  );
}
