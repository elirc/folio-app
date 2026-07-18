# 0006 — Query-time broken-link handling for page links

**Status:** Accepted

## Context

Blocks can link to other pages with an inline `#[Title](page-guid)` token. Target pages
can be renamed, trashed, restored, or made private after a link is created. We need
backlinks ("who links here") and outgoing links with a "broken" indicator, without
maintaining a fragile web of foreign keys that would fight soft-delete.

## Decision

- **Materialize links from content, resolve status at query time.** On every block
  create/update, `SyncLinksAsync` re-parses the content and rebuilds that block's
  `PageLink` rows. The `SourceBlock` FK **cascades** (deleting a block drops its links),
  but the **target is a plain id with no FK**, so a link may dangle deliberately.
- **Broken is computed on read, not stored.** `GET /api/pages/{id}/links` marks a link
  `IsBroken` when the target doesn't resolve to a live (non-trashed) page — so trashing a
  target breaks the link and restoring it heals the link automatically, no bookkeeping.
- **Visibility filtering (privacy).** Backlinks are filtered by `CanSeeVisibility` so a
  private source never leaks to a non-Owner. Outgoing links apply the same filter to
  *targets*: a target the caller can't see is reported broken with only the stored token
  title (already visible in the source block's own text), so the target's **current**
  title never leaks.

## Consequences

- Link status is always consistent with the live page graph with zero migration/cleanup
  jobs; delete→restore heals links for free.
- Resolving status is a per-request join rather than a stored flag (fine at this scale).
- The visibility filter on outgoing links was a bug fix: it previously returned a private
  target's live title to readers who couldn't see the page. Covered by `LinkLeakTests`.
