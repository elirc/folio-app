# Architecture Decision Records

Short records of the non-obvious decisions in Folio — the context, the decision, and
what it costs. They document *why* the code is the way it is, reverse-engineered from the
implementation.

| # | Decision | Status |
| - | -------- | ------ |
| [0001](0001-jwt-pbkdf2-auth.md) | JWT bearer auth with in-house PBKDF2 password hashing | Accepted |
| [0002](0002-404-vs-403.md) | 404 for cross-workspace resources, 403 for visible-but-forbidden | Accepted |
| [0003](0003-migrate-not-ensurecreated.md) | Apply migrations at startup (not EnsureCreated) + a drift guard | Accepted |
| [0004](0004-json-block-payloads.md) | Store block content as opaque JSON payloads | Accepted |
| [0005](0005-version-snapshot-restore.md) | Version history as full snapshots; non-destructive restore | Accepted |
| [0006](0006-query-time-broken-links.md) | Query-time broken-link handling for page links | Accepted |
| [0007](0007-notification-targeting.md) | Notification targeting given a workspace-level favorite flag | Accepted |
| [0008](0008-utc-datetime-storage.md) | Store all timestamps as UTC `DateTime` | Accepted |
| [0009](0009-optimistic-concurrency.md) | Optimistic concurrency via an explicit `Guid Version` | Accepted |
| [0010](0010-vite-5-pin.md) | Pin Vite 5 for Vitest 2 compatibility | Accepted |
