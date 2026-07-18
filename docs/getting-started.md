# Getting started

Run both halves locally and walk through the core features. Everything below was
verified against a fresh checkout.

## Prerequisites

| Tool | Version |
| ---- | ------- |
| .NET SDK | 10.0.x |
| Node.js | 22.x |
| pnpm | 9.x (`corepack enable`) |

## 1. Run the server

```bash
cd server
dotnet run --project Folio.Api        # http://localhost:5080
```

On startup the API applies EF migrations and (in Development) seeds sample data into
`server/Folio.Api/folio.db`. Confirm it's healthy:

```bash
curl http://localhost:5080/health
# {"status":"ok","database":"up","timestamp":"2026-…Z"}
```

> The connection string defaults to `Data Source=folio.db` (a file in the API project
> directory). Delete that file to reset to a fresh seed. Override it with
> `ConnectionStrings__Folio` if you want the DB elsewhere — use a **Windows-style path**
> (e.g. `C:\tmp\folio.db`), not a POSIX path.

## 2. Run the client

```bash
cd client
pnpm install
pnpm dev                              # http://localhost:5173
```

The Vite dev server proxies `/api/*` and `/health` to `http://localhost:5080`, so keep
the server running alongside it. Open <http://localhost:5173> and sign in.

## Seed accounts

All seeded members share the password **`password`**:

| Email | Role | Workspace |
| ----- | ---- | --------- |
| `ada@acme.test` | Owner | Acme Docs |
| `grace@acme.test` | Editor | Acme Docs |
| `linus@acme.test` | Viewer | Acme Docs |
| `eve@globex.test` | Owner | Globex (isolated — used to demonstrate cross-workspace 404s) |

The Acme workspace seeds a small page tree (Getting Started → Installation,
Configuration; Engineering → Architecture, Runbooks; Product), with the Product page
already shared via a public link (`/api/public/pages/acme-product-roadmap`).

## Walkthrough (via the API)

The whole product loop, driven with `curl`. Sign in as the Owner first:

```bash
BASE=http://localhost:5080
TOKEN=$(curl -s -X POST $BASE/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"ada@acme.test","password":"password"}' | \
  python -c "import sys,json;print(json.load(sys.stdin)['token'])")
AUTH="Authorization: Bearer $TOKEN"
WS=11111111-1111-1111-1111-111111111111    # Acme workspace id (stable seed)
```

**Create a page** (201 Created):

```bash
PAGE=$(curl -s -X POST $BASE/api/workspaces/$WS/pages -H "$AUTH" \
  -H 'Content-Type: application/json' -d '{"title":"Walkthrough Page"}')
PGID=$(echo "$PAGE" | python -c "import sys,json;print(json.load(sys.stdin)['id'])")
```

**Add a block**:

```bash
curl -s -X POST $BASE/api/pages/$PGID/blocks -H "$AUTH" \
  -H 'Content-Type: application/json' \
  -d '{"type":"Paragraph","content":{"text":"hello walkthrough"}}'
```

**Share it publicly** (mints a `publicSlug`; the page is then readable anonymously):

```bash
curl -s -X PUT $BASE/api/pages/$PGID/share -H "$AUTH" \
  -H 'Content-Type: application/json' \
  -d '{"visibility":"Public","permission":"View"}'
# {"visibility":"Public","permission":"View","publicSlug":"0dcc2bdbd225"}

curl -s $BASE/api/public/pages/0dcc2bdbd225        # no token needed → 200
```

**Comment** (mention a teammate with `@[Name](memberId)` — get ids from
`GET /api/workspaces/$WS/members`):

```bash
curl -s -X POST $BASE/api/pages/$PGID/comments -H "$AUTH" \
  -H 'Content-Type: application/json' -d '{"body":"first comment"}'
```

**Search** and **export**:

```bash
curl -s "$BASE/api/workspaces/$WS/search?q=Walkthrough" -H "$AUTH"
# [{"pageId":"…","title":"Walkthrough Page","matchedTitle":true,…}]

curl -s "$BASE/api/pages/$PGID/export" -H "$AUTH"
# {"filename":"walkthrough-page.md","markdown":"# Walkthrough Page\n\nhello walkthrough"}
```

**Permissions in action**: the same requests as `linus@acme.test` (Viewer) return
`403` for writes (create/edit/share) but `200` for reads; requests as
`eve@globex.test` for an Acme page return `404` (cross-workspace existence is hidden);
a request with no token returns `401`.

## Running the tests

```bash
cd server && dotnet test        # xUnit integration suite (in-memory SQLite)
cd client && pnpm test          # Vitest component suite (fetch stubbed — no server needed)
cd client && pnpm build         # tsc -b && vite build
```

See [testing.md](testing.md) for the full test taxonomy and harness details.
