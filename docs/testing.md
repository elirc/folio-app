# Testing

Two suites, both fast and hermetic (no external server, no on-disk database).

| Suite | Framework | Count |
| ----- | --------- | ----- |
| Server | xUnit + `WebApplicationFactory` (in-memory SQLite) | **150** |
| Client | Vitest + React Testing Library (jsdom + fetch stubs) | **45** |

## Running everything

```bash
# server
cd server
dotnet build Folio.slnx
dotnet test  Folio.slnx        # 150 passing

# client
cd client
pnpm test                      # 45 passing (vitest run)
pnpm build                     # tsc -b && vite build (also the typecheck gate)
```

Filter server tests by name: `dotnet test --filter "FullyQualifiedName~MigrationGuardTests"`.
Run one client file: `pnpm exec vitest run src/components/PermissionGating.test.tsx`.

## Server taxonomy

Almost every server test is an **integration test**: it boots the real API through
`FolioApiFactory` and exercises HTTP endpoints end-to-end (routing, auth, validation,
services, EF, SQLite). A handful are **pure unit / model tests** (e.g.
`MigrationGuardTests`) that build a `FolioDbContext` directly.

`Folio.Tests` by area:

- `AuthTests`, `PermissionMatrixTests` — JWT login and the role × visibility × permission
  read/write matrix, foreign-workspace 404s, public-link access.
- `PageEndpointTests`, `TreeBlockEdgeTests` — page CRUD, tree move/reorder, cycle
  prevention, position clamping, orphan handling.
- `BlockEndpointTests` — typed blocks, v2 types, toggle nesting, cross-parent moves.
- `VersionTests`, `HistoryEdgeTests` — snapshots, diffs, non-destructive restore.
- `CommentTests`, `NotificationTests`, `MentionNotificationTests` — threads, mentions,
  fan-out, unread counts.
- `LinkTests`, `LinkLeakTests` — backlinks, broken-link handling, link privacy, export.
- `SharingSearchTests`, `SearchV2Tests`, `SearchPermissionTests` — sharing, search
  filters, quick-open ranking, visibility filtering.
- `TemplateTests` — templates, deep-copy duplicate, Markdown export.
- `ProductionReadinessTests`, `ConcurrencyEdgeTests` — optimistic concurrency (409),
  rate limiting (429) and recovery, interleaved reorders.
- `HardeningTests`, `PersistenceTests`, `HealthEndpointTests`, `MigrationGuardTests` —
  ProblemDetails, pagination, seeding, health probe, migration drift.

### `FolioApiFactory` harness

`FolioApiFactory` (a `WebApplicationFactory<Program>`) boots the **real** API against a
private **in-memory SQLite** database. A single `SqliteConnection("DataSource=:memory:")`
is opened and kept open for the factory's lifetime so the schema + seed persist across
the scoped `DbContext` instances the app creates per request.

- It runs in the **Development** environment, so `Program`'s startup path
  (`db.Database.Migrate()` then `DbSeeder.Seed(db)`) runs against that in-memory
  connection — i.e. tests execute against a **migrated** schema, the same path
  production uses.
- `CreateAuthenticatedClient(email)` logs in over the real `/api/auth/login` endpoint
  and attaches the bearer token, so the whole auth path is exercised.
- `WithDbAsync(...)` runs an assertion against a fresh scoped `FolioDbContext`.
- `WritePermitLimit` / `WriteWindowSeconds` init-properties tune the rate limiter for
  the throttling tests.

**Isolation pattern**: read-only tests share one factory via `IClassFixture<FolioApiFactory>`;
tests that mutate state create a `new FolioApiFactory()` per test (and dispose it), so
mutations never leak between cases.

## The EnsureCreated-vs-Migrate caveat + drift guard

A common trap: test harnesses that build the schema with `EnsureCreated()` bypass the
migration pipeline entirely, so a model change with **no matching migration** still
"works" in tests while breaking a freshly-migrated production database. That gap has
shipped a missing migration in a sibling repo.

Folio avoids it two ways:

1. **The harness migrates.** As above, `FolioApiFactory` boots the real `Program`, which
   calls `Migrate()` — not `EnsureCreated()` — on the in-memory connection. Tests run
   against the migrated schema.
2. **`MigrationGuardTests`** adds two explicit guards:
   - *`Model_matches_the_migrations_snapshot_no_pending_changes`* diffs the live EF model
     against the migrations' model snapshot (`IMigrationsModelDiffer`) and fails if there
     are **any** pending changes — i.e. the model was edited without
     `dotnet ef migrations add`.
   - *`Seeder_runs_against_a_migrated_database`* creates a fresh SQLite DB, calls
     `Migrate()` then `DbSeeder.Seed()`, and asserts the expected row counts — pinning
     that the seeder is compatible with the migrated schema and that seeding is
     idempotent.

The drift guard was validated by temporarily adding an unmapped entity property: both
guard tests failed (the diff test on the pending change, the seeder test on the
missing column), confirming the guard catches real drift. The probe was then removed;
the model has **no drift** today.

### Adding a migration

When you change an entity, generate a migration so the guard stays green:

```bash
cd server
dotnet ef migrations add <Name> \
  --project Folio.Infrastructure --startup-project Folio.Api
```

A design-time `FolioDbContextFactory` lets the EF tooling build the context without
running the API's migrate/seed startup.

## Client taxonomy

Component tests render a single component (or small tree) with React Testing Library in
jsdom and assert on the accessible DOM. **No server runs** — `globalThis.fetch` is stubbed.

Helpers in `client/src/test/`:

- `fetchMock.ts` — `installFetchMock(routes)` resolves `"METHOD /path"` (or `"/path"`)
  to a mocked `Response` and returns the `vi.fn()` so tests can assert requests. `204`
  responses default to a **null body** per the Fetch spec.
- `renderWithRouter.tsx` — renders inside a `MemoryRouter`.
- `authTestUtils.ts` — `seedSession()` primes a signed-in session in `localStorage` +
  the API client; `clearSession()` tears it down.
- `setup.ts` — RTL cleanup after each test.

Coverage highlights: routing/auth guard, login, sidebar tree, block editor + v2 types +
nesting, share dialog, comments, history, backlinks, templates, trash, search,
quick-open keyboard flow, notification inbox mark-read, and the read-only
(`PermissionGating`) UI states for the Viewer role.
