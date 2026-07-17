import { useEffect, useState } from "react";
import type { Block } from "../api/types";
import { deleteBlock, moveBlock, updateBlock } from "../api/folio";

interface BlockItemProps {
  block: Block;
  index: number;
  total: number;
  onChanged: () => void;
}

export function BlockItem({ block, index, total, onChanged }: BlockItemProps) {
  const [text, setText] = useState(block.content.text ?? "");

  useEffect(() => {
    setText(block.content.text ?? "");
  }, [block.id, block.content.text]);

  async function saveText() {
    if (text === (block.content.text ?? "")) {
      return;
    }
    await updateBlock(block.id, { type: block.type, content: { ...block.content, text } });
    onChanged();
  }

  async function toggleChecked() {
    await updateBlock(block.id, {
      type: block.type,
      content: { ...block.content, checked: !block.content.checked },
    });
    onChanged();
  }

  async function remove() {
    await deleteBlock(block.id);
    onChanged();
  }

  async function move(delta: number) {
    await moveBlock(block.id, index + delta);
    onChanged();
  }

  const isCode = block.type === "Code";
  const level = block.type === "Heading" ? Math.min(Math.max(block.content.level ?? 1, 1), 3) : undefined;

  return (
    <div className={`block block-${block.type.toLowerCase()}`} data-testid="block" data-block-type={block.type}>
      <div className="block-gutter">
        <button
          type="button"
          className="icon-btn"
          aria-label="Move block up"
          disabled={index === 0}
          onClick={() => move(-1)}
        >
          ↑
        </button>
        <button
          type="button"
          className="icon-btn"
          aria-label="Move block down"
          disabled={index === total - 1}
          onClick={() => move(1)}
        >
          ↓
        </button>
        <button type="button" className="icon-btn danger" aria-label="Delete block" onClick={remove}>
          ×
        </button>
      </div>

      <div className="block-main">
        {block.type === "Todo" && (
          <input
            type="checkbox"
            className="block-check"
            aria-label="Toggle to-do"
            checked={Boolean(block.content.checked)}
            onChange={toggleChecked}
          />
        )}
        {block.type === "Bulleted" && <span className="block-bullet" aria-hidden>•</span>}
        {isCode && <span className="block-lang">{block.content.language ?? "text"}</span>}

        <textarea
          className={
            "block-text" +
            (isCode ? " code" : "") +
            (level ? ` heading h${level}` : "") +
            (block.type === "Todo" && block.content.checked ? " done" : "")
          }
          aria-label={`${block.type} text`}
          rows={isCode ? 3 : 1}
          value={text}
          placeholder={isCode ? "code…" : "Type something…"}
          onChange={(e) => setText(e.target.value)}
          onBlur={saveText}
        />
      </div>
    </div>
  );
}
