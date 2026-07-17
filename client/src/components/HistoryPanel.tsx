import { useState } from "react";
import type { VersionDetail, VersionSummary } from "../api/types";
import { getVersion, getVersions, restoreVersion, saveVersion } from "../api/folio";
import { useAsync } from "../hooks/useAsync";

interface HistoryPanelProps {
  pageId: string;
  /** Called after a save or restore so the page + blocks can refresh. */
  onChanged: () => void;
  onClose: () => void;
}

export function HistoryPanel({ pageId, onChanged, onClose }: HistoryPanelProps) {
  const { data, error, loading, reload } = useAsync<VersionSummary[]>(
    (signal) => getVersions(pageId, signal),
    [pageId],
  );
  const [busy, setBusy] = useState(false);
  const [expanded, setExpanded] = useState<number | null>(null);
  const [detail, setDetail] = useState<VersionDetail | null>(null);

  async function save() {
    setBusy(true);
    try {
      await saveVersion(pageId);
      reload();
      onChanged();
    } finally {
      setBusy(false);
    }
  }

  async function restore(versionNumber: number) {
    setBusy(true);
    try {
      await restoreVersion(pageId, versionNumber);
      reload();
      onChanged();
    } finally {
      setBusy(false);
    }
  }

  async function toggleDiff(versionNumber: number) {
    if (expanded === versionNumber) {
      setExpanded(null);
      setDetail(null);
      return;
    }
    setExpanded(versionNumber);
    setDetail(null);
    setDetail(await getVersion(pageId, versionNumber));
  }

  return (
    <aside className="history-panel" aria-label="Page history">
      <div className="history-header">
        <span className="sidebar-title">History</span>
        <div className="history-actions">
          <button type="button" className="add-block-btn" onClick={save} disabled={busy}>
            Save version
          </button>
          <button type="button" className="icon-btn" aria-label="Close history" onClick={onClose}>
            ×
          </button>
        </div>
      </div>

      {loading && <p className="muted">Loading history…</p>}
      {error && <p className="error-text">Could not load history.</p>}
      {data && data.length === 0 && <p className="muted">No versions saved yet.</p>}
      {data && data.length > 0 && (
        <ul className="history-list">
          {data.map((version) => (
            <li key={version.versionNumber} className="history-item">
              <div className="history-row">
                <button
                  type="button"
                  className="history-version"
                  onClick={() => toggleDiff(version.versionNumber)}
                  aria-expanded={expanded === version.versionNumber}
                >
                  <span className="history-num">v{version.versionNumber}</span>
                  <span className="history-title">{version.title}</span>
                  <span className="muted history-meta">
                    {version.blockCount} blocks
                    {version.createdByName ? ` · ${version.createdByName}` : ""}
                  </span>
                  {version.label && <span className="history-label">{version.label}</span>}
                </button>
                <button
                  type="button"
                  className="add-block-btn"
                  onClick={() => restore(version.versionNumber)}
                  disabled={busy}
                >
                  Restore
                </button>
              </div>
              {expanded === version.versionNumber && detail && (
                <p className="history-diff">
                  Changes vs current: <span className="diff-added">+{detail.diff.added}</span>{" "}
                  <span className="diff-removed">−{detail.diff.removed}</span>{" "}
                  <span className="diff-changed">~{detail.diff.changed}</span>
                </p>
              )}
            </li>
          ))}
        </ul>
      )}
    </aside>
  );
}
