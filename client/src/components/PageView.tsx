import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import type { PageDetail } from "../api/types";
import { favoritePage, getPage, renamePage, unfavoritePage } from "../api/folio";
import { useAsync } from "../hooks/useAsync";
import { BacklinksPanel } from "./BacklinksPanel";
import { BlockList } from "./BlockList";
import { CommentSidebar } from "./CommentSidebar";
import { HistoryPanel } from "./HistoryPanel";
import { ShareDialog } from "./ShareDialog";

interface PageViewProps {
  pageId: string;
  workspaceId: string;
  onChanged: () => void;
}

export function PageView({ pageId, workspaceId, onChanged }: PageViewProps) {
  const { data, error, loading, reload } = useAsync<PageDetail>(
    (signal) => getPage(pageId, signal),
    [pageId],
  );

  const [title, setTitle] = useState("");
  const [shareOpen, setShareOpen] = useState(false);
  const [historyOpen, setHistoryOpen] = useState(false);
  const [commentsOpen, setCommentsOpen] = useState(false);
  const [linksOpen, setLinksOpen] = useState(false);
  // Bumping this remounts BlockList so it refetches after a version restore.
  const [blocksNonce, setBlocksNonce] = useState(0);
  useEffect(() => {
    if (data) {
      setTitle(data.title);
    }
    setShareOpen(false);
    setHistoryOpen(false);
    setCommentsOpen(false);
    setLinksOpen(false);
  }, [data]);

  function onHistoryChanged() {
    reload();
    setBlocksNonce((n) => n + 1);
    onChanged();
  }

  async function saveTitle() {
    const trimmed = title.trim();
    if (!data || trimmed.length === 0 || trimmed === data.title) {
      if (data) {
        setTitle(data.title);
      }
      return;
    }
    await renamePage(pageId, { title: trimmed, icon: data.icon });
    reload();
    onChanged();
  }

  async function toggleFavorite() {
    if (!data) {
      return;
    }
    if (data.isFavorite) {
      await unfavoritePage(pageId);
    } else {
      await favoritePage(pageId);
    }
    reload();
    onChanged();
  }

  if (loading) {
    return <p className="muted">Loading page…</p>;
  }
  if (error || !data) {
    return <p className="error-text">Could not load this page.</p>;
  }

  return (
    <article className="page-view">
      <div className="page-toolbar">
        <nav className="breadcrumb" aria-label="Breadcrumb">
          {data.breadcrumb.map((crumb, index) => (
            <span key={crumb.id}>
              {index > 0 && <span className="breadcrumb-sep"> / </span>}
              {crumb.id === data.id ? (
                <span className="breadcrumb-current">{crumb.title}</span>
              ) : (
                <Link to={`/w/${workspaceId}/p/${crumb.id}`}>{crumb.title}</Link>
              )}
            </span>
          ))}
        </nav>
        <div className="page-toolbar-actions">
          <button
            type="button"
            className="icon-btn"
            aria-label={data.isFavorite ? "Remove from favorites" : "Add to favorites"}
            aria-pressed={data.isFavorite}
            onClick={toggleFavorite}
          >
            {data.isFavorite ? "★" : "☆"}
          </button>
          <button type="button" className="share-btn" onClick={() => setCommentsOpen((v) => !v)}>
            Comments
          </button>
          <button type="button" className="share-btn" onClick={() => setLinksOpen((v) => !v)}>
            Links
          </button>
          <button type="button" className="share-btn" onClick={() => setHistoryOpen((v) => !v)}>
            History
          </button>
          <button type="button" className="share-btn" onClick={() => setShareOpen((v) => !v)}>
            Share
          </button>
        </div>
      </div>

      {shareOpen && (
        <ShareDialog page={data} onChanged={reload} onClose={() => setShareOpen(false)} />
      )}

      {historyOpen && (
        <HistoryPanel
          pageId={data.id}
          onChanged={onHistoryChanged}
          onClose={() => setHistoryOpen(false)}
        />
      )}

      {commentsOpen && (
        <CommentSidebar
          pageId={data.id}
          workspaceId={workspaceId}
          onClose={() => setCommentsOpen(false)}
        />
      )}

      {linksOpen && (
        <BacklinksPanel
          pageId={data.id}
          workspaceId={workspaceId}
          onClose={() => setLinksOpen(false)}
        />
      )}

      <input
        className="page-title-input"
        aria-label="Page title"
        value={title}
        onChange={(e) => setTitle(e.target.value)}
        onBlur={saveTitle}
        onKeyDown={(e) => {
          if (e.key === "Enter") {
            e.currentTarget.blur();
          }
        }}
      />

      <div className="page-body">
        <BlockList key={blocksNonce} pageId={data.id} workspaceId={workspaceId} />
      </div>
    </article>
  );
}
