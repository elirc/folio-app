import { useEffect, useState } from "react";
import type { Block, BlockContent, BlockType, LinkTarget } from "../api/types";
import { createBlock, deleteBlock, moveBlock, updateBlock } from "../api/folio";
import { AddBlockBar } from "./AddBlockBar";
import { BlockTree } from "./BlockTree";

interface BlockItemProps {
  block: Block;
  index: number;
  total: number;
  pageId: string;
  byParent: Map<string | null, Block[]>;
  linkTargets: LinkTarget[];
  onChanged: () => void;
}

/** Block types whose text can carry an inline page link. */
const TEXT_TYPES = new Set(["Paragraph", "Heading", "Todo", "Bulleted", "Quote", "Code", "Toggle", "Callout"]);

export function BlockItem({ block, index, total, pageId, byParent, linkTargets, onChanged }: BlockItemProps) {
  const [text, setText] = useState(block.content.text ?? "");

  useEffect(() => {
    setText(block.content.text ?? "");
  }, [block.id, block.content.text]);

  async function patch(content: BlockContent) {
    await updateBlock(block.id, { type: block.type, content });
    onChanged();
  }

  async function saveText() {
    if (text === (block.content.text ?? "")) {
      return;
    }
    await patch({ ...block.content, text });
  }

  const toggleChecked = () => patch({ ...block.content, checked: !block.content.checked });
  const toggleCollapsed = () => patch({ ...block.content, collapsed: !block.content.collapsed });

  async function remove() {
    await deleteBlock(block.id);
    onChanged();
  }

  async function move(delta: number) {
    await moveBlock(block.id, { position: index + delta, parentId: block.parentBlockId });
    onChanged();
  }

  async function addChild(type: BlockType, content: BlockContent) {
    await createBlock(pageId, { type, content, parentId: block.id });
    onChanged();
  }

  async function insertLink(target: LinkTarget) {
    const token = `#[${target.title}](${target.id})`;
    const next = text.length > 0 ? `${text} ${token}` : token;
    setText(next);
    await patch({ ...block.content, text: next });
  }

  const gutter = (
    <div className="block-gutter">
      <button type="button" className="icon-btn" aria-label="Move block up" disabled={index === 0} onClick={() => move(-1)}>
        ↑
      </button>
      <button type="button" className="icon-btn" aria-label="Move block down" disabled={index === total - 1} onClick={() => move(1)}>
        ↓
      </button>
      <button type="button" className="icon-btn danger" aria-label="Delete block" onClick={remove}>
        ×
      </button>
    </div>
  );

  return (
    <div className={`block block-${block.type.toLowerCase()}`} data-testid="block" data-block-type={block.type}>
      {gutter}
      <div className="block-main">
        <BlockBody
          block={block}
          text={text}
          setText={setText}
          saveText={saveText}
          toggleChecked={toggleChecked}
          toggleCollapsed={toggleCollapsed}
          patch={patch}
        />
        {TEXT_TYPES.has(block.type) && linkTargets.length > 0 && (
          <LinkPicker targets={linkTargets} onPick={insertLink} />
        )}
        {block.type === "Toggle" && !block.content.collapsed && (
          <div className="toggle-children">
            <BlockTree
              pageId={pageId}
              blocks={byParent.get(block.id) ?? []}
              byParent={byParent}
              linkTargets={linkTargets}
              onChanged={onChanged}
            />
            <AddBlockBar onAdd={addChild} label="Add block inside toggle" />
          </div>
        )}
      </div>
    </div>
  );
}

interface BlockBodyProps {
  block: Block;
  text: string;
  setText: (value: string) => void;
  saveText: () => void;
  toggleChecked: () => void;
  toggleCollapsed: () => void;
  patch: (content: BlockContent) => Promise<void>;
}

/** Renders the type-specific editor for a single block. */
function BlockBody({ block, text, setText, saveText, toggleChecked, toggleCollapsed, patch }: BlockBodyProps) {
  switch (block.type) {
    case "Divider":
      return <hr className="block-divider" aria-label="Divider" />;

    case "Image":
      return <ImageBlock block={block} patch={patch} />;

    case "Table":
      return <TableBlock block={block} patch={patch} />;

    case "Callout":
      return (
        <>
          <span className="block-emoji" aria-hidden>
            {block.content.emoji ?? "💡"}
          </span>
          <TextArea block={block} text={text} setText={setText} saveText={saveText} placeholder="Callout…" />
        </>
      );

    case "Toggle":
      return (
        <>
          <button
            type="button"
            className="toggle-caret"
            aria-label={block.content.collapsed ? "Expand toggle" : "Collapse toggle"}
            aria-expanded={!block.content.collapsed}
            onClick={toggleCollapsed}
          >
            {block.content.collapsed ? "▸" : "▾"}
          </button>
          <TextArea block={block} text={text} setText={setText} saveText={saveText} placeholder="Toggle heading…" />
        </>
      );

    case "Todo":
      return (
        <>
          <input
            type="checkbox"
            className="block-check"
            aria-label="Toggle to-do"
            checked={Boolean(block.content.checked)}
            onChange={toggleChecked}
          />
          <TextArea block={block} text={text} setText={setText} saveText={saveText} placeholder="Type something…" />
        </>
      );

    case "Bulleted":
      return (
        <>
          <span className="block-bullet" aria-hidden>
            •
          </span>
          <TextArea block={block} text={text} setText={setText} saveText={saveText} placeholder="Type something…" />
        </>
      );

    case "Code":
      return (
        <>
          <span className="block-lang">{block.content.language ?? "text"}</span>
          <TextArea block={block} text={text} setText={setText} saveText={saveText} placeholder="code…" code rows={3} />
        </>
      );

    default:
      return <TextArea block={block} text={text} setText={setText} saveText={saveText} placeholder="Type something…" />;
  }
}

interface TextAreaProps {
  block: Block;
  text: string;
  setText: (value: string) => void;
  saveText: () => void;
  placeholder: string;
  code?: boolean;
  rows?: number;
}

function TextArea({ block, text, setText, saveText, placeholder, code, rows }: TextAreaProps) {
  const level =
    block.type === "Heading" ? Math.min(Math.max(block.content.level ?? 1, 1), 3) : undefined;
  const className =
    "block-text" +
    (code ? " code" : "") +
    (level ? ` heading h${level}` : "") +
    (block.type === "Todo" && block.content.checked ? " done" : "");

  return (
    <textarea
      className={className}
      aria-label={`${block.type} text`}
      rows={rows ?? 1}
      value={text}
      placeholder={placeholder}
      onChange={(e) => setText(e.target.value)}
      onBlur={saveText}
    />
  );
}

function LinkPicker({ targets, onPick }: { targets: LinkTarget[]; onPick: (target: LinkTarget) => void }) {
  return (
    <select
      className="link-picker"
      aria-label="Insert page link"
      value=""
      onChange={(e) => {
        const target = targets.find((t) => t.id === e.target.value);
        if (target) {
          onPick(target);
        }
      }}
    >
      <option value="">🔗 Link…</option>
      {targets.map((t) => (
        <option key={t.id} value={t.id}>
          {t.title}
        </option>
      ))}
    </select>
  );
}

function ImageBlock({ block, patch }: { block: Block; patch: (content: BlockContent) => Promise<void> }) {
  const [url, setUrl] = useState(block.content.url ?? "");
  const [alt, setAlt] = useState(block.content.alt ?? "");

  useEffect(() => {
    setUrl(block.content.url ?? "");
    setAlt(block.content.alt ?? "");
  }, [block.id, block.content.url, block.content.alt]);

  function save() {
    if (url === (block.content.url ?? "") && alt === (block.content.alt ?? "")) {
      return;
    }
    void patch({ ...block.content, url, alt });
  }

  return (
    <div className="image-block">
      {url ? (
        <img className="image-preview" src={url} alt={alt} />
      ) : (
        <div className="image-placeholder">No image URL yet</div>
      )}
      <div className="image-fields">
        <input
          className="block-input"
          aria-label="Image URL"
          placeholder="https://…"
          value={url}
          onChange={(e) => setUrl(e.target.value)}
          onBlur={save}
        />
        <input
          className="block-input"
          aria-label="Image alt text"
          placeholder="Alt text"
          value={alt}
          onChange={(e) => setAlt(e.target.value)}
          onBlur={save}
        />
      </div>
    </div>
  );
}

function TableBlock({ block, patch }: { block: Block; patch: (content: BlockContent) => Promise<void> }) {
  const [rows, setRows] = useState<string[][]>(block.content.rows ?? []);

  useEffect(() => {
    setRows(block.content.rows ?? []);
  }, [block.id, block.content.rows]);

  function editCell(r: number, c: number, value: string) {
    setRows((current) => {
      const next = current.map((row) => [...row]);
      next[r][c] = value;
      return next;
    });
  }

  // Persist the edited grid on blur so a burst of typing is one request.
  const save = () => void patch({ ...block.content, rows });

  function addRow() {
    const width = rows[0]?.length ?? 1;
    void patch({ ...block.content, rows: [...rows.map((row) => [...row]), Array(width).fill("")] });
  }

  function addColumn() {
    void patch({ ...block.content, rows: rows.map((row) => [...row, ""]) });
  }

  return (
    <div className="table-block">
      <table className="block-table">
        <tbody>
          {rows.map((row, r) => (
            <tr key={r}>
              {row.map((cell, c) => (
                <td key={c}>
                  <input
                    className="table-cell"
                    aria-label={`Cell ${r + 1},${c + 1}`}
                    value={cell}
                    onChange={(e) => editCell(r, c, e.target.value)}
                    onBlur={save}
                  />
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
      <div className="table-actions">
        <button type="button" className="add-block-btn" onClick={addRow}>
          + Row
        </button>
        <button type="button" className="add-block-btn" onClick={addColumn}>
          + Column
        </button>
      </div>
    </div>
  );
}
