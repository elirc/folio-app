# 0010 — Pin Vite 5 for Vitest 2 compatibility

**Status:** Accepted

## Context

The client builds and tests with the Vite toolchain: **Vite** for dev/build and
**Vitest** for the component suite (jsdom + React Testing Library). Vitest shares Vite's
plugin/transform pipeline, so the two must be on compatible major versions. Vitest 2
targets Vite 5; jumping Vite to 6 ahead of the matching Vitest can break the transform
pipeline and the test runner.

## Decision

Pin **Vite 5** (and `@vitejs/plugin-react` for that line) alongside **Vitest 2**. Treat
Vite and Vitest as a coupled pair and upgrade them together, not independently. The client
`build` script (`tsc -b && vite build`) doubles as the TypeScript typecheck gate.

## Consequences

- `pnpm build` and `pnpm test` share one known-good transform pipeline; no runner/plugin
  version skew.
- Vite major upgrades are deferred until Vitest ships matching support — upgrade both in
  the same change and re-run the full client suite.
- The dev server (port 5173) proxies `/api` and `/health` to the API (5080); tests never
  hit the network (fetch is stubbed), so the pin only affects build/test tooling, not
  runtime behavior.
