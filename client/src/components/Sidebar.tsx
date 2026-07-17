import { useNavigate } from "react-router-dom";
import type { PageTreeNode } from "../api/types";
import { createPage, deletePage } from "../api/folio";
import { PageTreeItem } from "./PageTreeItem";

interface SidebarProps {
  workspaceId: string;
  tree: PageTreeNode[];
  activePageId?: string;
  onChanged: () => void;
}

export function Sidebar({ workspaceId, tree, activePageId, onChanged }: SidebarProps) {
  const navigate = useNavigate();

  async function addRootPage() {
    const page = await createPage(workspaceId, { title: "Untitled" });
    onChanged();
    navigate(`/w/${workspaceId}/p/${page.id}`);
  }

  async function addChild(parentId: string) {
    const page = await createPage(workspaceId, { title: "Untitled", parentId });
    onChanged();
    navigate(`/w/${workspaceId}/p/${page.id}`);
  }

  async function removePage(pageId: string) {
    await deletePage(pageId);
    onChanged();
    if (activePageId === pageId) {
      navigate(`/w/${workspaceId}`);
    }
  }

  return (
    <nav className="sidebar" aria-label="Pages">
      <div className="sidebar-header">
        <span className="sidebar-title">Pages</span>
        <button type="button" className="icon-btn" onClick={addRootPage} aria-label="New page">
          +
        </button>
      </div>
      {tree.length === 0 ? (
        <p className="sidebar-empty">No pages yet.</p>
      ) : (
        <ul className="tree" role="tree">
          {tree.map((node) => (
            <PageTreeItem
              key={node.id}
              node={node}
              workspaceId={workspaceId}
              activePageId={activePageId}
              onAddChild={addChild}
              onDelete={removePage}
            />
          ))}
        </ul>
      )}
    </nav>
  );
}
