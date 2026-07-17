import { NavLink } from "react-router-dom";
import type { PageTreeNode } from "../api/types";

interface FavoritesListProps {
  workspaceId: string;
  tree: PageTreeNode[];
}

function flatten(nodes: PageTreeNode[]): PageTreeNode[] {
  return nodes.flatMap((node) => [node, ...flatten(node.children)]);
}

export function FavoritesList({ workspaceId, tree }: FavoritesListProps) {
  const favorites = flatten(tree).filter((node) => node.isFavorite);

  if (favorites.length === 0) {
    return null;
  }

  return (
    <div className="favorites">
      <span className="sidebar-title">Favorites</span>
      <ul className="favorites-list">
        {favorites.map((node) => (
          <li key={node.id}>
            <NavLink
              to={`/w/${workspaceId}/p/${node.id}`}
              className={({ isActive }) => (isActive ? "tree-link active" : "tree-link")}
            >
              <span className="tree-icon" aria-hidden>
                {node.icon ?? "📄"}
              </span>
              <span className="tree-label">{node.title}</span>
            </NavLink>
          </li>
        ))}
      </ul>
    </div>
  );
}
