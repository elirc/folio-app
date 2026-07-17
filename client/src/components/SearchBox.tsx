import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import type { SearchResult } from "../api/types";
import { searchPages } from "../api/folio";

interface SearchBoxProps {
  workspaceId: string;
}

export function SearchBox({ workspaceId }: SearchBoxProps) {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<SearchResult[]>([]);
  const [open, setOpen] = useState(false);
  const navigate = useNavigate();

  useEffect(() => {
    const q = query.trim();
    if (q.length === 0) {
      setResults([]);
      setOpen(false);
      return;
    }

    const controller = new AbortController();
    const timer = setTimeout(() => {
      searchPages(workspaceId, q, controller.signal)
        .then((r) => {
          setResults(r);
          setOpen(true);
        })
        .catch(() => {
          /* aborted or failed: leave prior results */
        });
    }, 200);

    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [query, workspaceId]);

  function goTo(pageId: string) {
    setOpen(false);
    setQuery("");
    navigate(`/w/${workspaceId}/p/${pageId}`);
  }

  return (
    <div className="search-box">
      <input
        type="search"
        className="search-input"
        aria-label="Search pages"
        placeholder="Search pages…"
        value={query}
        onChange={(e) => setQuery(e.target.value)}
      />
      {open && (
        <ul className="search-results" role="listbox" aria-label="Search results">
          {results.length === 0 ? (
            <li className="search-empty">No matches</li>
          ) : (
            results.map((r) => (
              <li key={r.pageId}>
                <button type="button" className="search-result" onClick={() => goTo(r.pageId)}>
                  <span className="search-title">
                    {r.icon ?? "📄"} {r.title}
                  </span>
                  {r.snippet && <span className="search-snippet">{r.snippet}</span>}
                </button>
              </li>
            ))
          )}
        </ul>
      )}
    </div>
  );
}
