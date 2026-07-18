# 0007 — Notification targeting given a workspace-level favorite flag

**Status:** Accepted

## Context

Comments should notify the people who care: the page author, prior participants in the
thread, and anyone explicitly `@`-mentioned. A tempting fourth audience is "people who
favorited the page" — but `Page.IsFavorite` is a single **workspace-level boolean**, not a
per-member relationship. There is no per-user favorite table to target.

## Decision

- **Target from relationships we actually have.** `ActivityService.FanOutCommentAsync`
  fans a comment out to **mentioned members ∪ the page author ∪ prior commenters on the
  page**, minus the actor. Favorites are deliberately *not* a notification input, because
  the schema can't attribute a favorite to a member.
- **Exactly-once, no self-notify.** Recipients are collected in a `HashSet`, so someone
  who qualifies several ways (author *and* mentioned *and* a prior commenter) gets exactly
  one notification; the actor is removed so you're never notified about your own comment.
- **Same transaction as the mutation.** Activity + notification rows are added to the
  `DbContext` and persisted by the calling service's single `SaveChangesAsync`, so a
  comment and its notifications commit atomically.

## Consequences

- Notification targeting is correct and race-free without a per-user favorites table.
- "Notify me about pages I starred" is intentionally unsupported — it would require a
  schema change (a member↔page favorites join). The shared-favorite flag stays as a
  simple UI affordance and is **not** treated as a bug.
- Covered by `MentionNotificationTests` (exactly-once fan-out, no self-notify, mark-read
  idempotency) and `NotificationTests`.
