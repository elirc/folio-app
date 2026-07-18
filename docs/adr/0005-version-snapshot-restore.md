# 0005 — Version history as full snapshots; non-destructive restore

**Status:** Accepted

## Context

Pages need a history users can browse, diff, and roll back. Two broad designs: store
**deltas** (compact, but replaying/branching is complex) or store **full snapshots**
(simple, more storage). Restore must not silently destroy the state a user is reverting
*from*.

## Decision

- **Full snapshots.** `PageVersionService.SnapshotAsync` captures the page's title/icon
  and its entire block set as a JSON blob (`PageVersion.BlocksJson`), numbered per page
  (`VersionNumber`, unique on `(PageId, VersionNumber)`).
- **Diff computed on read.** A version's diff against the current page is derived by block
  `Id`: *added* (present now, not in the version), *removed* (in the version, gone now),
  *changed* (same id, different type/position/parent/content).
- **Non-destructive restore.** `RestoreAsync` first snapshots the *current* state into a
  new version labelled `Before restore to vN`, then replaces the page's blocks with the
  target snapshot. Snapshot block **ids are reused on restore**, so toggle-nesting parent
  references survive.

## Consequences

- History is easy to reason about and each version is independently viewable; restore can
  always be undone because the pre-restore state was snapshotted.
- Storage grows with every snapshot (full copy each time) — acceptable for this app;
  history reads are capped at 200 versions.
- Restore is a write (`403` for Viewers) and rotates block rows; `HistoryEdgeTests` covers
  restore-of-a-restore and nested-toggle round-trips.
