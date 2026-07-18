# 0008 — Store all timestamps as UTC `DateTime`

**Status:** Accepted

## Context

The app orders and filters by time constantly — recent pages, activity feeds,
newest-first notifications, version history, `updatedAfter`/`updatedBefore` search
filters. The natural .NET type for an instant is `DateTimeOffset`, but the provider is
SQLite.

## Decision

Store every timestamp as a **UTC `DateTime`**, never `DateTimeOffset`. SQLite has no
native date/time type and **cannot correctly order or compare `DateTimeOffset`** values
(they round-trip as text that doesn't sort chronologically). All entities set timestamps
via `DateTime.UtcNow`; the seeder uses a fixed `DateTimeKind.Utc` instant for determinism.

## Consequences

- Ordering/range filters (`OrderByDescending(x => x.CreatedAt)`,
  `UpdatedAt >= after`) work correctly and translate to SQL.
- There is no per-row offset; callers treat all timestamps as UTC and localize in the UI.
- A guardrail worth remembering: **`[Required]` on a non-nullable `DateTime`/`Guid`/`enum`
  is a no-op** — request DTO fields that must be supplied are declared nullable so a
  missing value yields a `400` instead of binding a bogus default (e.g.
  `DateTime.MinValue`).
