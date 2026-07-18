# 0003 — Apply migrations at startup (not EnsureCreated) + a drift guard

**Status:** Accepted

## Context

EF Core offers two ways to materialize a schema: `EnsureCreated()` (build tables from the
current model, bypassing migrations) and `Migrate()` (apply the ordered migration
history). Mixing them is a classic footgun: if tests use `EnsureCreated()` but production
uses `Migrate()`, a model change with **no matching migration** passes the whole test
suite yet breaks a freshly-migrated production database. That exact gap has shipped a
missing migration in a sibling repo.

## Decision

- **Production and tests both `Migrate()`.** `Program` calls `db.Database.Migrate()` at
  startup (then seeds in Development). The integration harness (`FolioApiFactory`) boots
  that same `Program` against an in-memory SQLite connection, so tests run against the
  migrated schema too — no `EnsureCreated()` anywhere.
- **A drift guard in CI.** `MigrationGuardTests.Model_matches_the_migrations_snapshot…`
  diffs the live EF model against the migrations' model snapshot
  (`IMigrationsModelDiffer`) and fails on any pending change. A companion test migrates a
  fresh database and runs the seeder against it.

## Consequences

- Editing an entity without `dotnet ef migrations add` **fails the build**, caught before
  it can reach production.
- The guard needs the snapshot model finalized + runtime-initialized before diffing
  (`FinalizeModel()` → `IModelRuntimeInitializer.Initialize` → `GetRelationalModel()`);
  that boilerplate lives in the test.
- A design-time `FolioDbContextFactory` keeps `dotnet ef` from running the API's
  migrate/seed startup while generating migrations.
