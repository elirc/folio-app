import type { Block, BlockContent, BlockType } from "../api/types";
import { createBlock, getBlocks } from "../api/folio";
import { useAsync } from "../hooks/useAsync";
import { BlockItem } from "./BlockItem";

interface BlockListProps {
  pageId: string;
}

const ADD_OPTIONS: { type: BlockType; label: string; content: BlockContent }[] = [
  { type: "Paragraph", label: "Text", content: { text: "" } },
  { type: "Heading", label: "Heading", content: { text: "", level: 2 } },
  { type: "Todo", label: "To-do", content: { text: "", checked: false } },
  { type: "Bulleted", label: "Bullet", content: { text: "" } },
  { type: "Quote", label: "Quote", content: { text: "" } },
  { type: "Code", label: "Code", content: { text: "", language: "text" } },
];

export function BlockList({ pageId }: BlockListProps) {
  const { data, error, loading, reload } = useAsync<Block[]>(
    (signal) => getBlocks(pageId, signal),
    [pageId],
  );

  async function add(type: BlockType, content: BlockContent) {
    await createBlock(pageId, { type, content });
    reload();
  }

  if (loading) {
    return <p className="muted">Loading blocks…</p>;
  }
  if (error || !data) {
    return <p className="error-text">Could not load blocks.</p>;
  }

  return (
    <div className="block-list">
      {data.length === 0 ? (
        <p className="muted">No blocks yet — add one below.</p>
      ) : (
        data.map((block, index) => (
          <BlockItem
            key={block.id}
            block={block}
            index={index}
            total={data.length}
            onChanged={reload}
          />
        ))
      )}

      <div className="add-block-bar" role="toolbar" aria-label="Add block">
        {ADD_OPTIONS.map((option) => (
          <button
            key={option.type}
            type="button"
            className="add-block-btn"
            onClick={() => add(option.type, option.content)}
          >
            + {option.label}
          </button>
        ))}
      </div>
    </div>
  );
}
