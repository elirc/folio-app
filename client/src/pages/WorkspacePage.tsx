import { useEffect, useState } from "react";
import { Link, useLocation, useParams } from "react-router-dom";
import type { PageTreeNode } from "../api/types";
import { getPageTree } from "../api/folio";
import { useAuth } from "../auth/AuthContext";
import { useAsync } from "../hooks/useAsync";
import { Sidebar } from "../components/Sidebar";
import { PageView } from "../components/PageView";
import { SearchBox } from "../components/SearchBox";
import { FavoritesList } from "../components/FavoritesList";
import { TrashView } from "../components/TrashView";
import { TemplateGallery } from "../components/TemplateGallery";
import { SearchPage } from "../components/SearchPage";
import { QuickOpenModal } from "../components/QuickOpenModal";

export function WorkspacePage() {
  const { workspaceId = "", pageId } = useParams();
  const { member } = useAuth();
  // Viewers get a read-only page (the server enforces this too); Owners/Editors
  // see the edit affordances. Fine-grained per-page Editor permission is still
  // enforced server-side.
  const canEdit = member?.role !== "Viewer";
  const location = useLocation();
  const isTrash = location.pathname.endsWith("/trash");
  const isTemplates = location.pathname.endsWith("/templates");
  const isSearch = location.pathname.endsWith("/search");

  const [quickOpenVisible, setQuickOpenVisible] = useState(false);
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setQuickOpenVisible((v) => !v);
      }
    }
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);

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
        <Link to={`/w/${workspaceId}/search`} className="trash-link">
          🔎 Search
        </Link>
        <Link to={`/w/${workspaceId}/templates`} className="trash-link">
          📋 Templates
        </Link>
        <Link to={`/w/${workspaceId}/trash`} className="trash-link">
          🗑 Trash
        </Link>
      </aside>

      <section className="workspace-main">
        {isSearch ? (
          <SearchPage workspaceId={workspaceId} />
        ) : isTemplates ? (
          <TemplateGallery workspaceId={workspaceId} onChanged={reload} />
        ) : isTrash ? (
          <TrashView workspaceId={workspaceId} onChanged={reload} />
        ) : pageId ? (
          <PageView pageId={pageId} workspaceId={workspaceId} onChanged={reload} canEdit={canEdit} />
        ) : (
          <div className="empty-state">
            <h2>Select a page</h2>
            <p className="muted">Choose a page from the sidebar, or create a new one with “+”.</p>
          </div>
        )}
      </section>

      {quickOpenVisible && (
        <QuickOpenModal workspaceId={workspaceId} onClose={() => setQuickOpenVisible(false)} />
      )}
    </div>
  );
}
