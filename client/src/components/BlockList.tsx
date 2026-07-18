import type { Block, BlockContent, BlockType, LinkTarget, PageTreeNode } from "../api/types";
import { createBlock, getBlocks, getPageTree } from "../api/folio";
import { useAsync } from "../hooks/useAsync";
import { AddBlockBar } from "./AddBlockBar";
import { BlockTree } from "./BlockTree";

interface BlockListProps {
  pageId: string;
  workspaceId: string;
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

/** Flattens the page tree into a list of link targets, excluding the current page. */
export function flattenTargets(nodes: PageTreeNode[], excludePageId: string): LinkTarget[] {
  const out: LinkTarget[] = [];
  const walk = (list: PageTreeNode[]) => {
    for (const node of list) {
      if (node.id !== excludePageId) {
        out.push({ id: node.id, title: node.title, icon: node.icon });
      }
      walk(node.children);
    }
  };
  walk(nodes);
  return out;
}

export function BlockList({ pageId, workspaceId }: BlockListProps) {
  const { data, error, loading, reload } = useAsync<Block[]>(
    (signal) => getBlocks(pageId, signal),
    [pageId],
  );
  const tree = useAsync<PageTreeNode[]>((signal) => getPageTree(workspaceId, signal), [workspaceId]);

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
  const linkTargets = tree.data ? flattenTargets(tree.data, pageId) : [];

  return (
    <div className="block-list">
      {data.length === 0 ? (
        <p className="muted">No blocks yet — add one below.</p>
      ) : (
        <BlockTree
          pageId={pageId}
          blocks={roots}
          byParent={byParent}
          linkTargets={linkTargets}
          onChanged={reload}
        />
      )}

      <AddBlockBar onAdd={addRoot} />
    </div>
  );
}
