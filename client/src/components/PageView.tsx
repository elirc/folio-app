import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import type { PageDetail } from "../api/types";
import { getPage, renamePage } from "../api/folio";
import { useAsync } from "../hooks/useAsync";

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
  useEffect(() => {
    if (data) {
      setTitle(data.title);
    }
  }, [data]);

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

  if (loading) {
    return <p className="muted">Loading page…</p>;
  }
  if (error || !data) {
    return <p className="error-text">Could not load this page.</p>;
  }

  return (
    <article className="page-view">
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
        <p className="muted">This page has no blocks yet.</p>
      </div>
    </article>
  );
}
