# 0004 — Store block content as opaque JSON payloads

**Status:** Accepted

## Context

Folio has 11 block types (Heading, Paragraph, Todo, Bulleted, Quote, Code, Toggle,
Callout, Divider, Image, Table) whose data differs wildly: a Heading has `text` + `level`,
a Todo has `text` + `checked`, a Table has a `rows` grid, a Divider has nothing. Modeling
each as its own column/table would mean a wide sparse schema or many joins, and every new
block type would need a migration.

## Decision

A `Block` stores its payload as a single **JSON string** (`Block.Content`, required). The
shape is defined by convention per `Type`; the API only enforces that the payload is a
JSON **object** (`CreateBlockRequest`/`UpdateBlockRequest` reject non-object content with
`400`). Rendering (Markdown export, previews) and the client interpret the shape by type.
Blocks are returned to the client as a flat pre-order DFS list carrying the raw JSON
object.

## Consequences

- New block types need **no migration** — just a new content convention and client
  renderer.
- The database can't validate or query *inside* a payload; search does a `LIKE` over the
  raw JSON text, and snippet extraction pulls the `text` field defensively.
- The client and server must agree on payload shapes out-of-band (there's no schema per
  type). Inline page links live *inside* text payloads as `#[Title](id)` tokens and are
  materialized separately (see [0006](0006-query-time-broken-links.md)).
