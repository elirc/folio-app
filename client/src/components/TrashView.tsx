import type { TrashItem } from "../api/types";
import { getTrash, restorePage } from "../api/folio";
import { useAsync } from "../hooks/useAsync";

interface TrashViewProps {
  workspaceId: string;
  onChanged: () => void;
}

export function TrashView({ workspaceId, onChanged }: TrashViewProps) {
  const { data, error, loading, reload } = useAsync<TrashItem[]>(
    (signal) => getTrash(workspaceId, signal),
    [workspaceId],
  );

  async function restore(pageId: string) {
    await restorePage(pageId);
    reload();
    onChanged();
  }

  if (loading) {
    return <p className="muted">Loading trash…</p>;
  }
  if (error || !data) {
    return <p className="error-text">Could not load trash.</p>;
  }

  return (
    <section className="trash-view">
      <h2>Trash</h2>
      {data.length === 0 ? (
        <p className="muted">Trash is empty.</p>
      ) : (
        <ul className="trash-list">
          {data.map((item) => (
            <li key={item.id} className="trash-item">
              <span className="trash-title">
                {item.icon ?? "📄"} {item.title}
              </span>
              <button type="button" className="add-block-btn" onClick={() => restore(item.id)}>
                Restore
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
