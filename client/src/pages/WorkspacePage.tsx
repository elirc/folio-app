import { useParams } from "react-router-dom";
import type { PageTreeNode } from "../api/types";
import { getPageTree } from "../api/folio";
import { useAsync } from "../hooks/useAsync";
import { Sidebar } from "../components/Sidebar";
import { PageView } from "../components/PageView";

export function WorkspacePage() {
  const { workspaceId = "", pageId } = useParams();
  const { data, error, loading, reload } = useAsync<PageTreeNode[]>(
    (signal) => getPageTree(workspaceId, signal),
    [workspaceId],
  );

  return (
    <div className="workspace">
      <aside className="workspace-sidebar">
        {loading && <p className="muted">Loading…</p>}
        {error && <p className="error-text">Could not load pages.</p>}
        {data && (
          <Sidebar
            workspaceId={workspaceId}
            tree={data}
            activePageId={pageId}
            onChanged={reload}
          />
        )}
      </aside>
      <section className="workspace-main">
        {pageId ? (
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
