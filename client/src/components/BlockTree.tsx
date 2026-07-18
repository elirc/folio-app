import type { Block, LinkTarget } from "../api/types";
import { BlockItem } from "./BlockItem";

interface BlockTreeProps {
  pageId: string;
  blocks: Block[];
  byParent: Map<string | null, Block[]>;
  linkTargets: LinkTarget[];
  onChanged: () => void;
}

/** Renders one level of sibling blocks in order; toggles recurse via BlockItem. */
export function BlockTree({ pageId, blocks, byParent, linkTargets, onChanged }: BlockTreeProps) {
  return (
    <>
      {blocks.map((block, index) => (
        <BlockItem
          key={block.id}
          block={block}
          index={index}
          total={blocks.length}
          pageId={pageId}
          byParent={byParent}
          linkTargets={linkTargets}
          onChanged={onChanged}
        />
      ))}
    </>
  );
}
