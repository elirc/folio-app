# 0009 — Optimistic concurrency via an explicit `Guid Version`

**Status:** Accepted

## Context

Two people can edit the same page or block concurrently. Last-write-wins would silently
clobber the earlier edit. SQL Server has `rowversion`/`timestamp` for optimistic
concurrency, but **SQLite has no native rowversion**, and we don't want to force every
existing caller to start sending a concurrency token.

## Decision

- **Explicit token.** `Page` and `Block` each carry a `Guid Version`, rotated to a new
  `Guid` on every successful write.
- **Opt-in check.** A write request may include `ExpectedVersion`. If present and it
  doesn't match the stored `Version`, the write is rejected with **409**; the client
  reloads and retries. If `ExpectedVersion` is **omitted**, the check is skipped
  (backward compatible — existing callers keep last-write-wins).

## Consequences

- Concurrent edits surface as a `409` the UI handles (reload the latest version), instead
  of a silent overwrite. The client sends the current `version` it loaded.
- The token is a plain column compared in application code, not a DB-enforced rowversion,
  so the check is only as strong as the read-modify-write staying within one request
  (true here — each mutation loads, compares, and saves in one service call).
- Reorder/move operations don't carry a version, so a stale content edit still conflicts
  independently of interleaved reorders. Covered by `ProductionReadinessTests` and
  `ConcurrencyEdgeTests`.
