import { getHealth } from "../api/client";
import { useAsync } from "../hooks/useAsync";

export function HomePage() {
  const { data, error, loading } = useAsync((signal) => getHealth(signal), []);

  return (
    <section className="page">
      <h1>Folio</h1>
      <p className="subtitle">A Notion-style collaborative docs &amp; knowledge base.</p>

      <div className="health-card" data-testid="health-card">
        <span className="health-label">API status:</span>{" "}
        {loading && <span data-testid="health-status">checking…</span>}
        {error && (
          <span data-testid="health-status" className="health-down">
            unreachable
          </span>
        )}
        {data && (
          <span data-testid="health-status" className="health-up">
            {data.status}
          </span>
        )}
      </div>
    </section>
  );
}
