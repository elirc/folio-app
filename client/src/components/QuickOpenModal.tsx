import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import type { QuickOpenResult } from "../api/types";
import { quickOpen } from "../api/folio";

interface QuickOpenModalProps {
  workspaceId: string;
  onClose: () => void;
}

export function QuickOpenModal({ workspaceId, onClose }: QuickOpenModalProps) {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<QuickOpenResult[]>([]);
  const [selected, setSelected] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);
  const navigate = useNavigate();

  useEffect(() => {
    inputRef.current?.focus();
  }, []);

  // Debounced fetch (also fires on mount with an empty query → recent pages).
  useEffect(() => {
    const controller = new AbortController();
    const timer = setTimeout(() => {
      quickOpen(workspaceId, query.trim(), controller.signal)
        .then((r) => {
          setResults(r);
          setSelected(0);
        })
        .catch(() => {
          /* aborted or failed */
        });
    }, 120);
    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [query, workspaceId]);

  function open(result: QuickOpenResult) {
    onClose();
    navigate(`/w/${workspaceId}/p/${result.pageId}`);
  }

  function onKeyDown(e: React.KeyboardEvent) {
    if (e.key === "ArrowDown") {
      e.preventDefault();
      setSelected((s) => Math.min(s + 1, results.length - 1));
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setSelected((s) => Math.max(s - 1, 0));
    } else if (e.key === "Enter") {
      e.preventDefault();
      if (results[selected]) {
        open(results[selected]);
      }
    } else if (e.key === "Escape") {
      e.preventDefault();
      onClose();
    }
  }

  return (
    <div className="quickopen-overlay" role="presentation" onClick={onClose}>
      <div
        className="quickopen"
        role="dialog"
        aria-label="Quick open"
        onClick={(e) => e.stopPropagation()}
      >
        <input
          ref={inputRef}
          type="text"
          className="quickopen-input"
          aria-label="Quick open search"
          placeholder="Jump to a page…"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          onKeyDown={onKeyDown}
        />
        <ul className="quickopen-results" role="listbox" aria-label="Quick open results">
          {results.length === 0 ? (
            <li className="quickopen-empty">No pages found</li>
          ) : (
            results.map((r, index) => (
              <li key={r.pageId} role="option" aria-selected={index === selected}>
                <button
                  type="button"
                  className={`quickopen-item${index === selected ? " active" : ""}`}
                  onMouseEnter={() => setSelected(index)}
                  onClick={() => open(r)}
                >
                  <span className="quickopen-icon" aria-hidden>
                    {r.icon ?? "📄"}
                  </span>
                  <span className="quickopen-title">{r.title}</span>
                </button>
              </li>
            ))
          )}
        </ul>
      </div>
    </div>
  );
}
