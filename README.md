# Folio

A Notion-style collaborative docs & knowledge base — full-stack monorepo.

- **`/server`** — ASP.NET Core Web API (.NET 10), EF Core + SQLite.
- **`/client`** — Vite + React + TypeScript (strict), React Router, a typed fetch API client.

Workspaces contain **pages** arranged in a nested tree; each page is a tree of typed
**blocks**. Pages support JWT auth + role/visibility permissions, version history,
comments & @mentions, inline page links & backlinks, templates & Markdown export,
notifications, filtered search + quick-open, sharing, favorites, and trash/restore.

## Documentation

Full docs live in [`docs/`](docs/):

- [**Architecture**](docs/architecture.md) — layout, layering, page/block model,
  permissions (incl. the shared-favorite caveat), history, notifications, links,
  concurrency, and SQLite choices.
- [**API reference**](docs/api-reference.md) — every endpoint: method, route, auth,
  request/response shapes, and error codes.
- [**Getting started**](docs/getting-started.md) — run both halves, seed accounts, and a
  login → page → blocks → share → comment → search → export walkthrough.
- [**Testing**](docs/testing.md) — test taxonomy, harnesses, and the migration-drift guard.
- [**ADRs**](docs/adr/README.md) — the non-obvious decisions and their trade-offs.

## Features

### v1 (PRs #1–#6)

- **Workspaces & members** — a workspace owns members (Owner/Editor/Viewer) and a page tree.
- **Page tree** — nested pages with ordered positions; create, rename, nest, move/reorder
  (cycle-safe), breadcrumb trail. Sidebar tree UI with inline title editing.
- **Typed blocks** — heading / paragraph / to-do / bulleted / quote / code, each with a
  JSON payload; per-page ordering, reorder, and an inline block editor.
- **Sharing** — page visibility (Private / Workspace / Public link) and access (View / Edit).
- **Search / favorites / trash-restore** — full-text with snippets, starred pages, soft delete.
- **Hardening** — RFC 7807 ProblemDetails everywhere, model validation, and pagination.

### v2 (PRs #7–#15)

- **Auth & authorization** — JWT bearer login for seeded members (salted PBKDF2). Every
  endpoint is authorized: foreign workspace → 404, private pages are Owner-only, writes need
  Owner or Editor+Edit, Viewers are read-only. Public-link pages read without a token.
- **Block types v2** — table, collapsible toggle (with nested children), callout, divider,
  and image blocks; block nesting with cycle-safe move within/between parents.
- **Page history** — append-only version snapshots (title + block set), a diff summary
  (added/removed/changed), and non-destructive restore.
- **Comments & mentions** — page- and block-level threads with replies and resolve/unresolve;
  `@[Name](id)` mentions parsed and stored as member references.
- **Backlinks & links** — inline `#[Title](id)` page-link references, a backlinks endpoint
  (who links here), and broken-link handling across page delete/restore.
- **Templates & export** — reusable page templates, deep-copy page duplication, and Markdown
  export of a page (and optionally its subtree).
- **Notifications & activity** — an activity log for page/block/comment mutations; per-user
  notifications fanned out from mentions + comments on pages you authored/participate in;
  unread counts + mark-read, with a header inbox.
- **Search v2 & quick-open** — search filters (author, in-favorites, date range) plus a
  keyboard-driven quick-open modal (title-prefix ranked) and recent pages.
- **Production readiness** — request logging, `/health` DB probe with a structured body,
  optimistic concurrency (409 on stale writes) on pages/blocks, and write rate limiting (429).

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
curl http://localhost:5080/health
# {"status":"ok","database":"up","timestamp":"2026-…Z"}   (503 + "degraded"/"down" if the DB is unreachable)
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

A summary follows; the **full reference** (request/response shapes + error codes per
endpoint) is in [`docs/api-reference.md`](docs/api-reference.md).

Base URL `http://localhost:5080`. All errors are RFC 7807 `application/problem+json`.

All endpoints below require a `Authorization: Bearer <jwt>` header **except** `GET /health`,
`POST /api/auth/login`, and `GET /api/public/pages/{slug}`.

| Method & path | Purpose |
| ------------- | ------- |
| `GET /health` | Liveness + DB probe (structured body) |
| `POST /api/auth/login` · `GET /api/auth/me` | JWT login · current member |
| `GET /api/workspaces` · `/{id}` | Workspace summaries (caller's own) |
| `GET /api/workspaces/{id}/members` | Members (for the @mention picker) |
| `GET /api/workspaces/{id}/pages/tree` | Nested page tree |
| `GET /api/workspaces/{id}/pages?page=&pageSize=` | Paginated recent pages |
| `POST /api/workspaces/{id}/pages` | Create a page |
| `GET /api/pages/{id}` · `/{id}/breadcrumb` | Page detail · breadcrumb |
| `PUT /api/pages/{id}` | Rename / set icon (`expectedVersion` → 409) |
| `POST /api/pages/{id}/move` | Nest / move / reorder |
| `DELETE /api/pages/{id}` · `POST /{id}/restore` | Trash · restore |
| `POST /api/pages/{id}/duplicate` | Deep-copy the page subtree |
| `GET /api/pages/{id}/export?subtree=` | Markdown export |
| `PUT /api/pages/{id}/share` | Set visibility + access |
| `POST` / `DELETE /api/pages/{id}/favorite` | Favorite / unfavorite |
| `GET /api/pages/{id}/blocks` · `POST` | List · create blocks (`parentId` nests) |
| `PUT /api/blocks/{id}` · `POST /{id}/move` · `DELETE` | Update (`expectedVersion` → 409) · reorder · delete |
| `GET`/`POST /api/pages/{id}/versions` · `GET /{n}` · `POST /{n}/restore` | History: list/snapshot · detail+diff · restore |
| `GET`/`POST /api/pages/{id}/comments` · `POST /api/comments/{id}/resolve`·`/unresolve` · `DELETE` | Comment threads |
| `GET /api/pages/{id}/backlinks` · `/links` | Backlinks · outgoing links (broken flagged) |
| `POST /api/pages/{id}/templates` · `GET`/`POST /{tid}/instantiate`/`DELETE /api/workspaces/{id}/templates` | Templates |
| `GET /api/notifications` · `/unread-count` · `POST /{id}/read` · `/read-all` | Notifications |
| `GET /api/workspaces/{id}/activity` | Activity feed |
| `GET /api/workspaces/{id}/search?q=&author=&favorites=&updatedAfter=&updatedBefore=` | Filtered search |
| `GET /api/workspaces/{id}/quick-open?q=` | Quick-open (title-prefix ranked / recent) |
| `GET /api/workspaces/{id}/favorites` · `/trash` | Favorites · trash lists |
| `GET /api/public/pages/{slug}` | Public-link page access (anonymous) |

Write endpoints are rate-limited per user (429 when exceeded).

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
- Grouping by a nullable key uses `ToLookup` (a `Dictionary` rejects a null key) — used for
  the page tree, the nested block tree, and pre-order DFS block/export ordering.
- **PBKDF2 password hashing lives in `Folio.Domain`** (not the API) so the seeder in
  `Folio.Infrastructure` can hash passwords without referencing the web project.
- **Optimistic concurrency** is an explicit `Guid Version` compared against a request's
  `ExpectedVersion` (SQLite has no rowversion); a mismatch returns **409**. Omitting
  `ExpectedVersion` skips the check (backward compatible).
- **Rate limiting** partitions by the authenticated user and only throttles writes; reads,
  `/api/auth/*`, `/api/public/*`, and `/health` are exempt. `UseRateLimiter` runs **after**
  `UseAuthentication` so the partition can read the user's id.

---

## Testing

| Suite | Count |
| ----- | ----- |
| Server (xUnit + WebApplicationFactory, in-memory SQLite) | 150 |
| Client (Vitest + React Testing Library, jsdom + fetch stubs) | 45 |

Seeded logins (all password `password`): `ada@acme.test` (Owner), `grace@acme.test` (Editor),
`linus@acme.test` (Viewer); plus an isolated `eve@globex.test` (Owner) workspace.

See [`docs/testing.md`](docs/testing.md) for the full test taxonomy, harnesses, and the
migration-drift guard.

---

## Sprint history

Built as sprint PRs on `main`. **`v1.0.0`** (PRs #1–#6):

1. **Scaffold** — monorepo, `/health`, React shell + typed client, both test harnesses.
2. **Domain + persistence** — workspaces, members, pages (tree), blocks; EF Core + migration + seed.
3. **Pages & tree** — page CRUD, nest/move/reorder, breadcrumb; client sidebar tree + page view.
4. **Blocks** — typed block CRUD + per-page ordering/reorder; client block editor.
5. **Sharing, search & favorites** — permissions, full-text search, favorites, trash/restore.
6. **Hardening** — ProblemDetails, validation, pagination, tests, docs; tagged `v1.0.0`.

**`v2.0.0`** (PRs #7–#15):

7. **Auth & authorization** — JWT login, workspace roles, page-permission enforcement everywhere.
8. **Block types v2** — table / toggle / callout / divider / image; block nesting + move semantics.
9. **Page history** — version snapshots, diff summary, non-destructive restore.
10. **Comments & mentions** — page/block threads, resolve, `@mentions` as references.
11. **Backlinks & links** — inline page links, backlinks, broken-link handling.
12. **Templates & export** — templates, deep-copy duplicate, Markdown export.
13. **Notifications & activity** — activity log, mention/comment notifications, unread inbox.
14. **Search v2 & quick-open** — filters (author/favorites/date), keyboard quick-open, recents.
15. **Production readiness** — request logging, health DB probe, optimistic concurrency (409),
    write rate limiting (429), pagination audit, docs; tagged `v2.0.0`.
