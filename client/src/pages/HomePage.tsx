import { Link } from "react-router-dom";
import type { WorkspaceSummary } from "../api/types";
import { getHealth } from "../api/client";
import { listWorkspaces } from "../api/folio";
import { useAsync } from "../hooks/useAsync";

export function HomePage() {
  const health = useAsync((signal) => getHealth(signal), []);
  const workspaces = useAsync<WorkspaceSummary[]>((signal) => listWorkspaces(signal), []);

  return (
    <section className="page">
      <h1>Folio</h1>
      <p className="subtitle">A Notion-style collaborative docs &amp; knowledge base.</p>

      <div className="health-card" data-testid="health-card">
        <span className="health-label">API status:</span>{" "}
        {health.loading && <span data-testid="health-status">checking…</span>}
        {health.error && (
          <span data-testid="health-status" className="health-down">
            unreachable
          </span>
        )}
        {health.data && (
          <span data-testid="health-status" className="health-up">
            {health.data.status}
          </span>
        )}
      </div>

      <h2>Workspaces</h2>
      {workspaces.loading && <p className="muted">Loading workspaces…</p>}
      {workspaces.error && <p className="error-text">Could not load workspaces.</p>}
      {workspaces.data && (
        <ul className="workspace-list">
          {workspaces.data.map((ws) => (
            <li key={ws.id}>
              <Link to={`/w/${ws.id}`} className="workspace-card">
                <span className="workspace-name">{ws.name}</span>
                <span className="muted">
                  {ws.pageCount} pages · {ws.memberCount} members
                </span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
