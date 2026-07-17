# Folio

A Notion-style collaborative docs & knowledge base — full-stack monorepo.

- **`/server`** — ASP.NET Core Web API (.NET 10), EF Core + SQLite.
- **`/client`** — Vite + React + TypeScript (strict), React Router, a typed fetch API client.

Workspaces contain **pages** arranged in a nested tree; each page is a list of typed
**blocks** (headings, paragraphs, to-dos, bullets, quotes, code). Pages support
sharing/permissions, full-text search, favorites, and trash/restore (soft delete).

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

## .NET gotchas respected in this codebase

- SQLite cannot order/compare `DateTimeOffset`; timestamps are stored as UTC `DateTime`
  (or UTC ticks via a `ValueConverter`).
- `[Required]` on a non-nullable `DateTimeOffset`/`Guid` is a no-op — request DTO fields
  that must be supplied are nullable so a missing value yields a `400`, not a bogus value.
- EF `Include` is applied before `Skip`/`Take`.
- Record-DTO validation attributes target the constructor parameter (`[property: ...]`).

---

## Sprint history

Built as six sprint PRs on `main`:

1. **Scaffold** — monorepo, `/health`, React shell + typed client, both test harnesses.
2. **Domain + persistence** — workspaces, members, pages (tree), blocks; EF Core + migration + seed.
3. **Pages & tree** — page CRUD, nest/move/reorder, breadcrumb; client sidebar tree + page view.
4. **Blocks** — typed block CRUD + per-page ordering/reorder; client block editor.
5. **Sharing, search & favorites** — permissions, full-text search, favorites, trash/restore.
6. **Hardening** — ProblemDetails, validation, pagination, tests, docs; tagged `v1.0.0`.
