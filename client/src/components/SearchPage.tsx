import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import type { Member, SearchFilters, SearchResult } from "../api/types";
import { getMembers, searchPagesFiltered } from "../api/folio";
import { useAsync } from "../hooks/useAsync";

interface SearchPageProps {
  workspaceId: string;
}

export function SearchPage({ workspaceId }: SearchPageProps) {
  const members = useAsync<Member[]>((signal) => getMembers(workspaceId, signal), [workspaceId]);

  const [query, setQuery] = useState("");
  const [author, setAuthor] = useState("");
  const [favorites, setFavorites] = useState(false);
  const [after, setAfter] = useState("");
  const [before, setBefore] = useState("");
  const [results, setResults] = useState<SearchResult[]>([]);
  const [searched, setSearched] = useState(false);

  const hasCriteria = query.trim().length > 0 || author || favorites || after || before;

  useEffect(() => {
    if (!hasCriteria) {
      setResults([]);
      setSearched(false);
      return;
    }
    const controller = new AbortController();
    const filters: SearchFilters = {
      author: author || null,
      favorites,
      updatedAfter: after ? new Date(after).toISOString() : null,
      updatedBefore: before ? new Date(before).toISOString() : null,
    };
    const timer = setTimeout(() => {
      searchPagesFiltered(workspaceId, query.trim(), filters, controller.signal)
        .then((r) => {
          setResults(r);
          setSearched(true);
        })
        .catch(() => {
          /* aborted or failed */
        });
    }, 200);
    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [workspaceId, query, author, favorites, after, before, hasCriteria]);

  return (
    <section className="search-page">
      <h2>Search</h2>

      <div className="search-filters">
        <input
          type="search"
          className="search-input"
          aria-label="Search query"
          placeholder="Search titles and content…"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
        />
        <label className="filter-field">
          <span>Author</span>
          <select aria-label="Filter by author" value={author} onChange={(e) => setAuthor(e.target.value)}>
            <option value="">Anyone</option>
            {(members.data ?? []).map((m) => (
              <option key={m.id} value={m.id}>
                {m.name}
              </option>
            ))}
          </select>
        </label>
        <label className="filter-check">
          <input
            type="checkbox"
            aria-label="Only favorites"
            checked={favorites}
            onChange={(e) => setFavorites(e.target.checked)}
          />
          <span>In favorites</span>
        </label>
        <label className="filter-field">
          <span>Updated after</span>
          <input type="date" aria-label="Updated after" value={after} onChange={(e) => setAfter(e.target.value)} />
        </label>
        <label className="filter-field">
          <span>Updated before</span>
          <input type="date" aria-label="Updated before" value={before} onChange={(e) => setBefore(e.target.value)} />
        </label>
      </div>

      {!hasCriteria && <p className="muted">Enter a query or pick a filter to search.</p>}
      {hasCriteria && searched && results.length === 0 && <p className="muted">No results.</p>}

      <ul className="search-page-results">
        {results.map((r) => (
          <li key={r.pageId} className="search-page-result">
            <Link to={`/w/${workspaceId}/p/${r.pageId}`} className="search-title">
              {r.icon ?? "📄"} {r.title}
            </Link>
            {r.snippet && <span className="search-snippet">{r.snippet}</span>}
          </li>
        ))}
      </ul>
    </section>
  );
}
