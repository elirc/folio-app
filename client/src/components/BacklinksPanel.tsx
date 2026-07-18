import { Link } from "react-router-dom";
import type { Backlink, OutgoingLink } from "../api/types";
import { getBacklinks, getOutgoingLinks } from "../api/folio";
import { useAsync } from "../hooks/useAsync";

interface BacklinksPanelProps {
  pageId: string;
  workspaceId: string;
  onClose: () => void;
}

export function BacklinksPanel({ pageId, workspaceId, onClose }: BacklinksPanelProps) {
  const backlinks = useAsync<Backlink[]>((signal) => getBacklinks(pageId, signal), [pageId]);
  const outgoing = useAsync<OutgoingLink[]>((signal) => getOutgoingLinks(pageId, signal), [pageId]);

  return (
    <aside className="links-panel" aria-label="Backlinks">
      <div className="links-header">
        <span className="sidebar-title">Links</span>
        <button type="button" className="icon-btn" aria-label="Close links" onClick={onClose}>
          ×
        </button>
      </div>

      <section className="links-group">
        <h4 className="links-group-title">Linked from ({backlinks.data?.length ?? 0})</h4>
        {backlinks.loading && <p className="muted">Loading…</p>}
        {backlinks.data && backlinks.data.length === 0 && (
          <p className="muted">No pages link here yet.</p>
        )}
        {backlinks.data && backlinks.data.length > 0 && (
          <ul className="links-list">
            {backlinks.data.map((b) => (
              <li key={`${b.sourcePageId}-${b.sourceBlockId}`}>
                <Link to={`/w/${workspaceId}/p/${b.sourcePageId}`} className="tree-link">
                  <span className="tree-icon" aria-hidden>
                    {b.sourcePageIcon ?? "📄"}
                  </span>
                  <span className="tree-label">{b.sourcePageTitle}</span>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="links-group">
        <h4 className="links-group-title">Links on this page ({outgoing.data?.length ?? 0})</h4>
        {outgoing.data && outgoing.data.length === 0 && (
          <p className="muted">This page has no links.</p>
        )}
        {outgoing.data && outgoing.data.length > 0 && (
          <ul className="links-list">
            {outgoing.data.map((l) => (
              <li key={`${l.targetPageId}-${l.sourceBlockId}`}>
                {l.isBroken ? (
                  <span className="link-broken" title="This page no longer exists">
                    ⚠ {l.targetTitle} (broken)
                  </span>
                ) : (
                  <Link to={`/w/${workspaceId}/p/${l.targetPageId}`} className="tree-link">
                    <span className="tree-icon" aria-hidden>
                      🔗
                    </span>
                    <span className="tree-label">{l.targetTitle}</span>
                  </Link>
                )}
              </li>
            ))}
          </ul>
        )}
      </section>
    </aside>
  );
}
