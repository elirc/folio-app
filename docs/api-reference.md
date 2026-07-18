# API reference

Base URL (dev): `http://localhost:5080`. All request/response bodies are JSON. Enums
are serialized as **strings** (e.g. `"Owner"`, `"Public"`, `"Heading"`).

## Authentication

Every endpoint requires an `Authorization: Bearer <jwt>` header **except**:

- `GET /health`
- `POST /api/auth/login`
- `GET /api/public/pages/{slug}`

Obtain a token from `POST /api/auth/login`. Tokens are HS256-signed and expire after
`Jwt:ExpiryHours` (default **12h**). A missing/invalid/expired token yields **401**.

## Errors

All errors are RFC 7807 `application/problem+json` with `status`, `title`, `detail`,
`instance` (request path), and a `traceId` extension. Model-validation failures (400)
additionally include an `errors` object keyed by field.

Common status codes across endpoints:

| Code | Meaning |
| ---- | ------- |
| 400 | Validation failure, or an invalid operation (e.g. a move that would create a cycle) |
| 401 | No/invalid bearer token |
| 403 | Authenticated + can see the resource, but role/permission is insufficient |
| 404 | Not found — **or** in another workspace / a private page hidden from a non-Owner (existence not leaked) |
| 409 | Optimistic-concurrency conflict (`ExpectedVersion` did not match) |
| 429 | Write rate limit exceeded |

The **authorization matrix** (who may read/write a page) is documented in
[architecture.md](architecture.md#permission-model).

---

## Health

### `GET /health`
Liveness + DB probe. Anonymous.

- **200** `{ "status": "ok", "database": "up", "timestamp": "2026-07-18T02:29:51.9Z" }`
- **503** `{ "status": "degraded", "database": "down", "timestamp": … }` if the DB is unreachable.

---

## Auth

### `POST /api/auth/login`  (anonymous)
Request `{ "email": string, "password": string }`.
- **200** `{ "token": string, "expiresAt": datetime, "member": Member }`
- **400** missing/invalid email or password fields.
- **401** unknown email or wrong password (same response for both, so accounts can't be probed).

`Member` = `{ id, workspaceId, name, email, role }` where `role` ∈ `Owner|Editor|Viewer`.

### `GET /api/auth/me`
- **200** the current `Member`.
- **401** if unauthenticated.

---

## Workspaces

All under `/api/workspaces`. A workspace other than the caller's own is **404**.

| Method & path | Purpose | Success | Errors |
| --- | --- | --- | --- |
| `GET /api/workspaces` | The caller's workspace summaries | 200 `WorkspaceSummary[]` | — |
| `GET /api/workspaces/{id}` | One workspace summary | 200 `WorkspaceSummary` | 404 |
| `GET /api/workspaces/{id}/members` | Members (for @mention picker) | 200 `Member[]` | 404 |
| `GET /api/workspaces/{id}/activity?limit=` | Activity feed (default 50, max 200) | 200 `Activity[]` | 404 |
| `GET /api/workspaces/{id}/trash` | Roots of trashed subtrees | 200 `TrashItem[]` | 404 |
| `GET /api/workspaces/{id}/favorites` | Favorited pages | 200 `Favorite[]` | 404 |
| `GET /api/workspaces/{id}/pages?page=&pageSize=` | Paginated recent pages | 200 `Paged<PageListItem>` | 400 (bad paging), 404 |

`WorkspaceSummary` = `{ id, name, slug, memberCount, pageCount, createdAt }`.
`Paged<T>` = `{ items: T[], page, pageSize, total, totalPages }`. Pagination:
`page ≥ 1`, `1 ≤ pageSize ≤ 100` (defaults 1 / 20); out of range → **400**. Recent pages
exclude private pages for non-Owners.

---

## Pages

| Method & path | Purpose | Success | Errors |
| --- | --- | --- | --- |
| `GET /api/workspaces/{workspaceId}/pages/tree` | Nested page tree | 200 `PageTreeNode[]` | 404 |
| `POST /api/workspaces/{workspaceId}/pages` | Create a page | 201 `PageDetail` | 400, 403 (Viewer), 404 |
| `GET /api/pages/{id}` | Page detail + breadcrumb | 200 `PageDetail` | 403, 404 |
| `GET /api/pages/{id}/breadcrumb` | Ancestor chain | 200 `Breadcrumb[]` | 403, 404 |
| `PUT /api/pages/{id}` | Rename / set icon | 200 `PageDetail` | 400, 403, 404, 409 |
| `POST /api/pages/{id}/move` | Nest / move / reorder | 200 `PageDetail` | 400 (cycle), 403, 404 |
| `DELETE /api/pages/{id}` | Trash (soft-delete) the subtree | 204 | 403, 404 |
| `POST /api/pages/{id}/restore` | Restore from trash | 200 `PageDetail` | 403, 404 |
| `POST /api/pages/{id}/duplicate` | Deep-copy the subtree | 201 `PageDetail` | 403, 404 |
| `GET /api/pages/{id}/export?subtree=` | Markdown export | 200 `Export` | 403, 404 |
| `PUT /api/pages/{id}/share` | Set visibility + access | 200 `Share` | 400, 403, 404 |
| `POST /api/pages/{id}/favorite` | Favorite (workspace-level) | 200 `PageDetail` | 403, 404 |
| `DELETE /api/pages/{id}/favorite` | Unfavorite | 200 `PageDetail` | 403, 404 |

**Requests**
- Create: `{ "title": string (req), "parentId": guid?, "position": int?, "icon": string? }`. Missing title → 400. `position` clamps into the sibling range; omitted → appended.
- Update: `{ "title": string (req), "icon": string?, "expectedVersion": guid? }`. `expectedVersion` mismatch → 409.
- Move: `{ "parentId": guid?, "position": int }`. `parentId=null` → root. Moving a page under itself or a descendant → 400.
- Share: `{ "visibility": "Private"|"Workspace"|"Public", "permission": "View"|"Edit" }`. Going `Public` mints a `publicSlug`; leaving `Public` clears it.

**Responses**
- `PageDetail` = `{ id, workspaceId, parentId, title, icon, position, visibility, permission, publicSlug, isFavorite, version, createdAt, updatedAt, breadcrumb: Breadcrumb[] }`.
- `PageTreeNode` = `{ id, parentId, title, icon, position, isFavorite, children: PageTreeNode[] }`.
- `Breadcrumb` = `{ id, title, icon }`. `Export` = `{ filename, markdown }`. `Share` = `{ visibility, permission, publicSlug }`.

---

## Blocks

| Method & path | Purpose | Success | Errors |
| --- | --- | --- | --- |
| `GET /api/pages/{pageId}/blocks` | Blocks (flat pre-order DFS) | 200 `Block[]` | 403, 404 |
| `POST /api/pages/{pageId}/blocks` | Create a block | 201 `Block` | 400, 403, 404 |
| `PUT /api/blocks/{id}` | Update content and/or type | 200 `Block` | 400, 403, 404, 409 |
| `POST /api/blocks/{id}/move` | Reorder / re-parent | 200 `Block` | 400 (cycle / non-Toggle parent), 403, 404 |
| `DELETE /api/blocks/{id}` | Delete block + its subtree | 204 | 403, 404 |

**Requests**
- Create: `{ "type": BlockType (req), "content": object (req), "position": int?, "parentId": guid? }`. `content` must be a JSON **object** (a string/number → 400). `parentId` must reference a **Toggle** on the same page, else 400.
- Update: `{ "type": BlockType?, "content": object (req), "expectedVersion": guid? }`. `expectedVersion` mismatch → 409.
- Move: `{ "position": int, "parentId": guid? }`. Into own descendant / a non-Toggle parent → 400.

`Block` = `{ id, pageId, parentBlockId, type, position, content: object, version, createdAt, updatedAt }`.
`BlockType` ∈ `Heading, Paragraph, Todo, Bulleted, Quote, Code, Toggle, Callout, Divider, Image, Table`.

---

## Page history

| Method & path | Purpose | Success | Errors |
| --- | --- | --- | --- |
| `POST /api/pages/{pageId}/versions` | Snapshot current state | 201 `VersionSummary` | 403, 404 |
| `GET /api/pages/{pageId}/versions` | List versions (newest first) | 200 `VersionSummary[]` | 403, 404 |
| `GET /api/pages/{pageId}/versions/{n}` | Version detail + diff | 200 `VersionDetail` | 403, 404 |
| `POST /api/pages/{pageId}/versions/{n}/restore` | Non-destructive restore | 200 `VersionSummary` | 403, 404 |

Snapshot/restore are **writes** (Viewers get 403); listing/detail need read access.
`VersionSummary` = `{ versionNumber, title, icon, blockCount, createdByName, label, createdAt }`.
`VersionDetail` adds `blocks: BlockSnapshot[]` and `diff: { added, removed, changed }`.
Restore first snapshots the current state (labelled `Before restore to vN`).

---

## Comments & mentions

| Method & path | Purpose | Success | Errors |
| --- | --- | --- | --- |
| `GET /api/pages/{pageId}/comments` | Comments (oldest first) | 200 `Comment[]` | 403, 404 |
| `POST /api/pages/{pageId}/comments` | Create comment/reply | 201 `Comment` | 400, 403, 404 |
| `POST /api/comments/{id}/resolve` | Resolve a thread | 200 `Comment` | 403, 404 |
| `POST /api/comments/{id}/unresolve` | Reopen a thread | 200 `Comment` | 403, 404 |
| `DELETE /api/comments/{id}` | Delete comment + replies | 204 | 403, 404 |

Commenting needs only **read** access (Viewers can comment). Deleting is allowed for the
comment's author; otherwise it needs page **write** access (403 otherwise).
Create request: `{ "body": string (req), "blockId": guid?, "parentCommentId": guid? }`.
`@[Name](member-guid)` tokens in the body are parsed into `mentions` (only ids that are
members of the workspace are kept). `Comment` = `{ id, pageId, blockId, parentCommentId,
authorMemberId, authorName, body, isResolved, resolvedAt, mentions: {memberId,name}[],
createdAt, updatedAt }`.

---

## Links & backlinks

| Method & path | Purpose | Success | Errors |
| --- | --- | --- | --- |
| `GET /api/pages/{pageId}/backlinks` | Pages that link here | 200 `Backlink[]` | 403, 404 |
| `GET /api/pages/{pageId}/links` | Outgoing links (broken flagged) | 200 `OutgoingLink[]` | 403, 404 |

`Backlink` = `{ sourcePageId, sourcePageTitle, sourcePageIcon, sourceBlockId }` — filtered
so private sources aren't leaked to non-Owners. `OutgoingLink` = `{ targetPageId,
targetTitle, isBroken, sourceBlockId }` — `isBroken` is true when the target is
gone/trashed **or** the caller cannot see it (its live title is never leaked).

---

## Templates

| Method & path | Purpose | Success | Errors |
| --- | --- | --- | --- |
| `POST /api/pages/{pageId}/templates` | Create a template from a page | 201 `Template` | 400, 403, 404 |
| `GET /api/workspaces/{workspaceId}/templates` | List templates | 200 `Template[]` | 404 |
| `POST /api/workspaces/{workspaceId}/templates/{templateId}/instantiate` | New page from template | 201 `PageDetail` | 400, 403 (Viewer), 404 |
| `DELETE /api/workspaces/{workspaceId}/templates/{templateId}` | Delete a template | 204 | 403 (Viewer), 404 |

Creating a template needs read access to the source page; instantiate/delete require a
non-Viewer role. Create request: `{ "name": string (req), "description": string? }`.
Instantiate request: `{ "parentId": guid? }`. `Template` = `{ id, workspaceId, name,
description, sourceTitle, sourceIcon, blockCount, createdByName, createdAt }`.

---

## Notifications & activity

| Method & path | Purpose | Success |
| --- | --- | --- |
| `GET /api/notifications` | Caller's notifications (newest first, ≤200) | 200 `Notification[]` |
| `GET /api/notifications/unread-count` | Unread count | 200 `{ count }` |
| `POST /api/notifications/{id}/read` | Mark one read | 204 / **404** if not the caller's |
| `POST /api/notifications/read-all` | Mark all read | 200 `{ count }` (number cleared) |

`Notification` = `{ id, type, pageId, pageTitle, summary, isRead, createdAt }`.
The workspace **activity feed** is `GET /api/workspaces/{id}/activity` (above);
`Activity` = `{ id, actorName, type, pageId, pageTitle, summary, createdAt }`.

---

## Search & quick-open

### `GET /api/workspaces/{workspaceId}/search`
Query params: `q` (term), `author` (guid), `favorites` (bool), `updatedAfter`,
`updatedBefore` (ISO datetimes). Returns hits over page titles + block text respecting
visibility and filters. With no term and no filters, returns `[]`.
- **200** `SearchResult[]` = `{ pageId, title, icon, matchedTitle, snippet, updatedAt }`.
- **404** foreign workspace.

### `GET /api/workspaces/{workspaceId}/quick-open?q=`
Title-prefix-ranked matches; an empty `q` returns up to 10 most-recent pages. Respects
visibility.
- **200** `QuickOpenResult[]` = `{ pageId, title, icon, updatedAt }`.
- **404** foreign workspace.

---

## Public links

### `GET /api/public/pages/{slug}`  (anonymous)
Resolves a page shared via public link.
- **200** `PageDetail` if the page is currently `Public`.
- **404** if the slug is unknown or the page is no longer `Public`.
