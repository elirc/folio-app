# Architecture

Folio is a Notion-style collaborative docs app: a **.NET 10 Web API** over EF Core +
SQLite, and a **Vite + React + TypeScript** client. This document describes how the
pieces fit together and the design decisions behind the trickier subsystems. For the
decisions themselves (with rationale + trade-offs) see [`adr/`](adr/README.md).

## Monorepo layout

```
folio-app/
├─ server/                      # .NET solution (Folio.slnx)
│  ├─ Folio.Api/                # Web API host
│  │  ├─ Program.cs             # pipeline, auth, rate limiting, /health, migrate+seed
│  │  ├─ Controllers/           # thin HTTP controllers → services
│  │  ├─ Services/              # business logic (PageService, BlockService, …)
│  │  ├─ Auth/                  # JWT issuing, CurrentMember, PageAuthorization
│  │  └─ Contracts/             # request/response record DTOs
│  ├─ Folio.Domain/             # entities, enums, PasswordHasher (no framework deps)
│  ├─ Folio.Infrastructure/     # FolioDbContext, migrations, DbSeeder
│  └─ Folio.Tests/              # xUnit + WebApplicationFactory integration tests
└─ client/
   └─ src/
      ├─ api/                   # typed fetch client (client.ts, folio.ts, types.ts)
      ├─ auth/                  # AuthContext, RequireAuth route guard
      ├─ components/            # layout + feature UI
      ├─ hooks/                 # useAsync data-fetching hook
      ├─ pages/                 # routed pages
      └─ test/                  # vitest setup + helpers (fetch stub, session)
```

## Server layering

Three projects with a strict dependency direction:

```
Folio.Api  ──►  Folio.Infrastructure  ──►  Folio.Domain
   │                                          ▲
   └──────────────────────────────────────────┘
```

- **Folio.Domain** — plain entities (`Page`, `Block`, `Member`, …), enums
  (`MemberRole`, `PageVisibility`, `SharePermission`, `BlockType`), and
  `Security/PasswordHasher`. No EF or ASP.NET references. `PasswordHasher` lives here
  so the seeder (in Infrastructure) can hash passwords without depending on the web
  project.
- **Folio.Infrastructure** — `FolioDbContext` (all model configuration in
  `OnModelCreating`), the EF migrations, `DbSeeder`, and `AddFolioInfrastructure` DI
  registration.
- **Folio.Api** — controllers, services, auth, and DTO contracts. Controllers are
  thin: they call a service and map a `ServiceResult<T>` status
  (`Success/NotFound/Forbidden/Conflict/Invalid`) to an HTTP status code. All
  business logic and authorization lives in the services.

Request flow: `Controller → Service → FolioDbContext`. The current caller is resolved
from JWT claims by `ICurrentMemberAccessor` (`Auth/CurrentMember.cs`) and injected into
services that need it.

## Client architecture

- **Routing** (`App.tsx`): a `RequireAuth` guard wraps the app shell; unauthenticated
  users are redirected to `/login`. `WorkspacePage` is the main surface (sidebar tree
  + page view + search/templates/trash/quick-open).
- **Auth** (`auth/AuthContext.tsx`): stores the session (`token` + `member`) in
  `localStorage` under `folio.auth`, primes the API client's bearer token, and drops
  the session on any `401`.
- **Data fetching** (`hooks/useAsync.ts`): a small hook that runs an async loader with
  an `AbortSignal` and exposes `{ data, error, loading, reload }`. Components own their
  own fetches; there is no global store.
- **API client** (`api/client.ts` + `api/folio.ts`): a typed `fetch` wrapper that
  attaches the bearer token, throws a typed `ApiError` (carrying the RFC 7807
  `ProblemDetails`) on non-2xx, and reads `204` responses as `void`. `folio.ts` is a
  flat set of endpoint functions.

## Page tree + block model

### Pages

A `Page` is a node in a workspace's tree via a self-referencing `ParentId`
(`Parent`/`Children`). Sibling order is an explicit zero-based `Position`. Roots have
`ParentId == null`.

- The tree is built in `PageService.GetTreeAsync` by grouping pages with
  `ToLookup(p => p.ParentId)` — a `Dictionary` cannot hold a `null` key, but roots
  need one, and the lookup indexer conveniently returns an empty sequence for parents
  with no children.
- Create/move/delete keep sibling `Position` values a contiguous `0..n-1` run
  (`Reindex`). Moves are clamped into range and are **cycle-safe**: a page cannot be
  moved under itself or any descendant (`DescendantIdsAsync`).
- The FK is configured `OnDelete(DeleteBehavior.Restrict)` so the database never
  silently cascades a subtree away; subtree removal is done explicitly in the service.

### Blocks

A `Block` belongs to a page and is itself a small tree: children may nest **only under
a `Toggle`** (`ParentBlockId`, validated in `BlockService.ValidateParentAsync`).
`Content` is a JSON string payload whose shape depends on `Type` (11 types: Heading,
Paragraph, Todo, Bulleted, Quote, Code, Toggle, Callout, Divider, Image, Table).

- Blocks are returned as a **flat, pre-order DFS list** (`OrderTree`): each parent
  immediately precedes its ordered children. The client re-nests via `ParentBlockId`.
- Deleting a block removes its whole subtree explicitly (the FK is `Restrict`).
- Moving a block reindexes both the source and target sibling groups and is cycle-safe.

## Permission model

Central rules live in `Auth/PageAuthorization.cs` and are shared by every service.
Access is evaluated against the caller's workspace + role and the page's visibility +
share permission, producing `Allowed` / `NotFound` / `Forbidden`:

| Situation | Read | Write |
| --------- | ---- | ----- |
| Different workspace | **404** (existence hidden) | **404** |
| `Private` page, non-Owner | **403** | **403** |
| `Workspace`/`Public`, Viewer | 200 | **403** (viewers never write) |
| `Workspace`/`Public`, Editor, share = `View` | 200 | **403** |
| `Workspace`/`Public`, Editor, share = `Edit` | 200 | 200 |
| Owner | 200 | 200 (always) |

- **404 vs 403**: a resource in another workspace is reported `404` so its existence
  isn't leaked; a visible-but-forbidden resource is `403`. See
  [ADR 0002](adr/0002-404-vs-403.md).
- **Public links**: a `Public` page gets a random `PublicSlug` and is readable
  anonymously at `GET /api/public/pages/{slug}`. Changing visibility away from `Public`
  clears the slug, immediately revoking the link.
- List/tree/search results are filtered by `CanSeeVisibility` so a non-Owner never even
  sees a private page in a listing.
- **Commenting and favoriting need only read access** (viewers can comment and star);
  creating pages, editing blocks, sharing, versioning, duplicating, and templating are
  writes.

### The shared-favorite caveat

`Page.IsFavorite` is a single **page-level boolean**, not a per-member relationship.
Favoriting a page stars it for the whole workspace. This is an intentional schema
simplification, not a bug — notification targeting works around it (below) rather than
depending on per-user favorites. See [ADR 0007](adr/0007-notification-targeting.md).

## History / versioning

`PageVersionService` implements append-only page history:

- A **snapshot** captures the page's title/icon and its full block set as a JSON blob
  (`PageVersion.BlocksJson`), numbered per page (`VersionNumber`, unique index on
  `(PageId, VersionNumber)`).
- **Diff** against the current page is computed by block `Id`: *added* = present now but
  not in the version, *removed* = in the version but gone now, *changed* = same id with
  a different type/position/parent/content.
- **Restore is non-destructive**: the current state is first snapshotted into a new
  version labelled `Before restore to vN`, then the page's blocks are replaced with the
  target snapshot. Snapshot block **ids are reused on restore**, so parent references
  (toggle nesting) survive intact. See [ADR 0005](adr/0005-version-snapshot-restore.md).

## Activity spine → notifications

`ActivityService` is a small write-side spine:

- `Add(...)` appends an `Activity` row to the `DbContext` **without saving** — the
  calling service's `SaveChangesAsync` persists it, so the activity is part of the same
  transaction as the mutation that produced it.
- `FanOutCommentAsync` creates `Notification` rows for a comment's recipients:
  **mentioned members ∪ the page author ∪ prior commenters on the page**, minus the
  actor. Recipients are deduplicated with a `HashSet`, so someone who qualifies several
  ways (author *and* mentioned *and* a prior commenter) is notified **exactly once**,
  and you are never notified about your own comment.
- Prior commenters are read from the database before the new comment is saved, so the
  in-flight comment doesn't count itself.

Notifications are per-recipient (`RecipientMemberId`), carry `IsRead`, and are queried
newest-first with an unread count. `MarkReadAsync` only touches the caller's own rows.

## Links / backlinks

Inline page links are written in block text as `#[Title](page-guid)` (analogous to the
`@[Name](member-guid)` mention token).

- On every block create/update, `BlockService.SyncLinksAsync` re-parses the content
  (`PageLinkParser`) and rebuilds that block's materialized `PageLink` rows. The
  `SourceBlock` FK cascades, so deleting a block removes its links; the **target is a
  plain id** with no FK, so links may dangle.
- **Backlinks** (`GET /api/pages/{id}/backlinks`) join links to live source pages (the
  soft-delete query filter hides trashed sources) and are filtered by `CanSeeVisibility`
  so a private source never leaks to a non-Owner.
- **Outgoing links** (`GET /api/pages/{id}/links`) flag each link `IsBroken` when its
  target is gone/trashed. A target the caller **cannot see** (e.g. a page since made
  private) is also treated as broken so its *current* title never leaks — the stored
  token title, which is already in the source block's visible text, is shown instead.
  This mirrors the backlink visibility filtering. See
  [ADR 0006](adr/0006-query-time-broken-links.md).

## Concurrency + rate limiting

- **Optimistic concurrency**: `Page` and `Block` each carry a `Guid Version`. A write
  may include `ExpectedVersion`; if it doesn't match the stored value the write is
  rejected with **409**. The version is rotated to a new `Guid` on every successful
  write. Omitting `ExpectedVersion` skips the check (backward compatible). SQLite has no
  native rowversion, hence the explicit token. See
  [ADR 0009](adr/0009-optimistic-concurrency.md).
- **Rate limiting** (`Program.cs`): a partitioned fixed-window limiter keyed by the
  authenticated user id. Only **writes** are throttled; GET/HEAD/OPTIONS, `/api/auth/*`,
  `/api/public/*`, and `/health` are exempt. Defaults are 300 permits / 10 s (overridable
  via `RateLimit:PermitLimit` and `RateLimit:WindowSeconds`). Exceeding the budget
  returns **429**. `UseRateLimiter` runs **after** `UseAuthentication` so the partition
  key can read the caller's id.

## SQLite storage choices

- **UTC `DateTime`, never `DateTimeOffset`** — SQLite cannot order/compare
  `DateTimeOffset`, so all timestamps are stored as UTC `DateTime`. See
  [ADR 0008](adr/0008-utc-datetime-storage.md).
- **Enums as strings** — `MemberRole`, `PageVisibility`, `SharePermission`, and
  `BlockType` use `HasConversion<string>()` so the database is human-readable and stable
  against enum reordering.
- **Block content as JSON strings** — a `Block.Content` is opaque JSON validated to be a
  JSON object at the API boundary. See [ADR 0004](adr/0004-json-block-payloads.md).
- **Soft delete** — `Page.IsDeleted` drives a global query filter
  (`HasQueryFilter(p => !p.IsDeleted)`); trash/restore paths opt out with
  `IgnoreQueryFilters()`.
- **Filtered unique index** — `PublicSlug` is unique only where non-null
  (`HasFilter("\"PublicSlug\" IS NOT NULL")`).
- **Migrations, not EnsureCreated** — the app applies migrations at startup
  (`db.Database.Migrate()`), then seeds in Development. A `MigrationGuardTests` guard
  fails the build if the model drifts from the migrations. See
  [testing.md](testing.md) and [ADR 0003](adr/0003-migrate-not-ensurecreated.md).

## Request pipeline (`Program.cs`, in order)

1. Structured per-request logging (method, path, status, elapsed ms).
2. `UseExceptionHandler` + `UseStatusCodePages` → RFC 7807 `application/problem+json`
   with a `traceId` and `instance` on every error.
3. CORS (`http://localhost:5173` / `127.0.0.1:5173`).
4. `UseAuthentication` → `UseAuthorization` → `UseRateLimiter`.
5. `GET /health` (DB probe) and the MVC controllers.
6. On startup: `Migrate()`, then `DbSeeder.Seed()` in Development.
