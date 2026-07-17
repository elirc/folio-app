import { Link, useLocation, useParams } from "react-router-dom";
import type { PageTreeNode } from "../api/types";
import { getPageTree } from "../api/folio";
import { useAsync } from "../hooks/useAsync";
import { Sidebar } from "../components/Sidebar";
import { PageView } from "../components/PageView";
import { SearchBox } from "../components/SearchBox";
import { FavoritesList } from "../components/FavoritesList";
import { TrashView } from "../components/TrashView";

export function WorkspacePage() {
  const { workspaceId = "", pageId } = useParams();
  const location = useLocation();
  const isTrash = location.pathname.endsWith("/trash");

  const { data, error, loading, reload } = useAsync<PageTreeNode[]>(
    (signal) => getPageTree(workspaceId, signal),
    [workspaceId],
  );

  return (
    <div className="workspace">
      <aside className="workspace-sidebar">
        <SearchBox workspaceId={workspaceId} />
        {loading && <p className="muted">Loading…</p>}
        {error && <p className="error-text">Could not load pages.</p>}
        {data && (
          <>
            <FavoritesList workspaceId={workspaceId} tree={data} />
            <Sidebar
              workspaceId={workspaceId}
              tree={data}
              activePageId={pageId}
              onChanged={reload}
            />
          </>
        )}
        <Link to={`/w/${workspaceId}/trash`} className="trash-link">
          🗑 Trash
        </Link>
      </aside>

      <section className="workspace-main">
        {isTrash ? (
          <TrashView workspaceId={workspaceId} onChanged={reload} />
        ) : pageId ? (
          <PageView pageId={pageId} workspaceId={workspaceId} onChanged={reload} />
        ) : (
          <div className="empty-state">
            <h2>Select a page</h2>
            <p className="muted">Choose a page from the sidebar, or create a new one with “+”.</p>
          </div>
        )}
      </section>
    </div>
  );
}
