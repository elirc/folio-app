import type { Block, BlockContent, BlockType } from "../api/types";
import { createBlock, getBlocks } from "../api/folio";
import { useAsync } from "../hooks/useAsync";
import { AddBlockBar } from "./AddBlockBar";
import { BlockTree } from "./BlockTree";

interface BlockListProps {
  pageId: string;
}

/** Groups a flat block list by parent so the tree can be rendered recursively. */
export function childrenByParent(blocks: Block[]): Map<string | null, Block[]> {
  const map = new Map<string | null, Block[]>();
  for (const block of blocks) {
    const key = block.parentBlockId;
    const list = map.get(key) ?? [];
    list.push(block);
    map.set(key, list);
  }
  for (const list of map.values()) {
    list.sort((a, b) => a.position - b.position);
  }
  return map;
}

export function BlockList({ pageId }: BlockListProps) {
  const { data, error, loading, reload } = useAsync<Block[]>(
    (signal) => getBlocks(pageId, signal),
    [pageId],
  );

  async function addRoot(type: BlockType, content: BlockContent) {
    await createBlock(pageId, { type, content });
    reload();
  }

  if (loading) {
    return <p className="muted">Loading blocks…</p>;
  }
  if (error || !data) {
    return <p className="error-text">Could not load blocks.</p>;
  }

  const byParent = childrenByParent(data);
  const roots = byParent.get(null) ?? [];

  return (
    <div className="block-list">
      {data.length === 0 ? (
        <p className="muted">No blocks yet — add one below.</p>
      ) : (
        <BlockTree pageId={pageId} blocks={roots} byParent={byParent} onChanged={reload} />
      )}

      <AddBlockBar onAdd={addRoot} />
    </div>
  );
}
