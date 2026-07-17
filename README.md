# Folio

A Notion-style collaborative docs & knowledge base — full-stack monorepo.

- **`/server`** — ASP.NET Core Web API (.NET 10), EF Core + SQLite.
- **`/client`** — Vite + React + TypeScript (strict), React Router, a typed fetch API client.

Workspaces contain **pages** arranged in a nested tree; each page is a list of typed
**blocks** (headings, paragraphs, to-dos, bullets, quotes, code). Pages support
sharing/permissions, full-text search, favorites, and trash/restore (soft delete).

## Features

- **Workspaces & members** — a workspace owns members (Owner/Editor/Viewer) and a page tree.
- **Page tree** — nested pages with ordered positions; create, rename, nest, move/reorder
  (cycle-safe), breadcrumb trail. Sidebar tree UI with inline title editing.
- **Typed blocks** — heading / paragraph / to-do / bulleted / quote / code, each with a
  JSON payload; per-page ordering, reorder, and an inline block editor.
- **Sharing** — page visibility (Private / Workspace / Public link) and access (View / Edit);
  public pages get a shareable slug.
- **Search** — full-text over page titles and block text, with snippets.
- **Favorites** — star pages; a Favorites section in the sidebar.
- **Trash / restore** — soft delete of a page subtree, a trash view, and restore.
- **Hardening** — RFC 7807 ProblemDetails everywhere, model validation, and pagination.

---

## Prerequisites

| Tool | Version used |
| ---- | ------------ |
| .NET SDK | 10.0.302 |
| Node.js | 22.x |
| pnpm | 9.x (`corepack enable` or `npm i -g pnpm`) |

The client uses **pnpm** (shared content-addressable store — no duplicated `node_modules`).

---

## Repository layout

```
folio-app/
├─ server/                 # .NET solution (Folio.slnx)
│  ├─ Folio.Api/           # Web API host: controllers, Program.cs, /health
│  ├─ Folio.Domain/        # Entities + domain contracts (no framework deps)
│  ├─ Folio.Infrastructure/# EF Core DbContext, migrations, seed
│  └─ Folio.Tests/         # xUnit + WebApplicationFactory integration tests
└─ client/                 # Vite React TS app
   └─ src/
      ├─ api/              # typed fetch client
      ├─ components/       # layout + shared UI
      ├─ hooks/            # data-fetching hooks
      ├─ pages/            # routed pages
      └─ test/             # vitest setup + helpers
```

---

## Running the server

```bash
cd server
dotnet run --project Folio.Api      # http://localhost:5080
```

Health probe:

```bash
curl http://localhost:5080/health   # {"status":"ok"}
```

### Server tests

```bash
cd server
dotnet test
```

---

## Running the client

```bash
cd client
pnpm install
pnpm dev                            # http://localhost:5173
```

The dev server proxies `/health` and `/api/*` to `http://localhost:5080`, so run the
server alongside it. The client's typed API client also honours a `VITE_API_BASE_URL`
env var if you prefer to point at the API directly.

### Client tests

```bash
cd client
pnpm test          # vitest run (jsdom)
pnpm build         # tsc -b && vite build
```

Client tests stub `fetch`, so **no running server is required**.

---

## API reference

Base URL `http://localhost:5080`. All errors are RFC 7807 `application/problem+json`.

| Method & path | Purpose |
| ------------- | ------- |
| `GET /health` | Liveness probe |
| `GET /api/workspaces` · `/{id}` | Workspace summaries |
| `GET /api/workspaces/{id}/pages/tree` | Nested page tree |
| `GET /api/workspaces/{id}/pages?page=&pageSize=` | Paginated recent pages |
| `POST /api/workspaces/{id}/pages` | Create a page |
| `GET /api/pages/{id}` · `/{id}/breadcrumb` | Page detail · breadcrumb |
| `PUT /api/pages/{id}` | Rename / set icon |
| `POST /api/pages/{id}/move` | Nest / move / reorder |
| `DELETE /api/pages/{id}` · `POST /{id}/restore` | Trash · restore |
| `PUT /api/pages/{id}/share` | Set visibility + access |
| `POST` / `DELETE /api/pages/{id}/favorite` | Favorite / unfavorite |
| `GET /api/pages/{id}/blocks` · `POST` | List · create blocks |
| `PUT /api/blocks/{id}` · `POST /{id}/move` · `DELETE` | Update · reorder · delete block |
| `GET /api/workspaces/{id}/search?q=` | Full-text search |
| `GET /api/workspaces/{id}/favorites` · `/trash` | Favorites · trash lists |
| `GET /api/public/pages/{slug}` | Public-link page access |

---

## .NET gotchas respected in this codebase

- SQLite cannot order/compare `DateTimeOffset`; timestamps are stored as UTC `DateTime`.
- `[Required]` on a non-nullable `DateTimeOffset`/`Guid`/`enum`/`struct` is a no-op — request
  DTO fields that must be supplied are declared nullable so a missing value yields a `400`,
  not a bogus default.
- Record-DTO validation attributes are placed on the **constructor parameters** (not with a
  `[property:]` target) — .NET 10 throws if it finds validation metadata on the generated
  properties instead.
- EF `Include` is applied **before** `Skip`/`Take` (see `PageService.GetRecentAsync`).
- Grouping by a nullable key uses `ToLookup` (a `Dictionary` rejects a null key).

---

## Testing

| Suite | Count |
| ----- | ----- |
| Server (xUnit + WebApplicationFactory, in-memory SQLite) | 34 |
| Client (Vitest + React Testing Library, jsdom + fetch stubs) | 16 |

---

## Sprint history

Built as six sprint PRs on `main`, released as `v1.0.0`:

1. **Scaffold** — monorepo, `/health`, React shell + typed client, both test harnesses.
2. **Domain + persistence** — workspaces, members, pages (tree), blocks; EF Core + migration + seed.
3. **Pages & tree** — page CRUD, nest/move/reorder, breadcrumb; client sidebar tree + page view.
4. **Blocks** — typed block CRUD + per-page ordering/reorder; client block editor.
5. **Sharing, search & favorites** — permissions, full-text search, favorites, trash/restore.
6. **Hardening** — ProblemDetails, validation, pagination, tests, docs; tagged `v1.0.0`.
