import { NavLink } from "react-router-dom";
import type { PageTreeNode } from "../api/types";

interface PageTreeItemProps {
  node: PageTreeNode;
  workspaceId: string;
  activePageId?: string;
  onAddChild: (parentId: string) => void;
  onDelete: (pageId: string) => void;
}

export function PageTreeItem({
  node,
  workspaceId,
  activePageId,
  onAddChild,
  onDelete,
}: PageTreeItemProps) {
  return (
    <li className="tree-item" role="treeitem" aria-selected={activePageId === node.id}>
      <div className="tree-row">
        <NavLink
          to={`/w/${workspaceId}/p/${node.id}`}
          className={({ isActive }) => (isActive ? "tree-link active" : "tree-link")}
        >
          <span className="tree-icon" aria-hidden>
            {node.icon ?? "📄"}
          </span>
          <span className="tree-label">{node.title}</span>
        </NavLink>
        <span className="tree-actions">
          <button
            type="button"
            className="icon-btn"
            title="Add subpage"
            aria-label={`Add subpage to ${node.title}`}
            onClick={() => onAddChild(node.id)}
          >
            +
          </button>
          <button
            type="button"
            className="icon-btn danger"
            title="Delete page"
            aria-label={`Delete ${node.title}`}
            onClick={() => onDelete(node.id)}
          >
            ×
          </button>
        </span>
      </div>
      {node.children.length > 0 && (
        <ul className="tree" role="group">
          {node.children.map((child) => (
            <PageTreeItem
              key={child.id}
              node={child}
              workspaceId={workspaceId}
              activePageId={activePageId}
              onAddChild={onAddChild}
              onDelete={onDelete}
            />
          ))}
        </ul>
      )}
    </li>
  );
}
