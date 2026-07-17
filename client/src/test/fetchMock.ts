import { vi } from "vitest";

export interface MockRoute {
  status?: number;
  json?: unknown;
}

/**
 * Installs a `globalThis.fetch` stub that resolves routes by "METHOD path"
 * or just "path". Returns the vi mock so tests can assert calls.
 * No running server is required.
 */
export function installFetchMock(routes: Record<string, MockRoute>) {
  const mock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = typeof input === "string" ? input : input.toString();
    const path = url.replace(/^https?:\/\/[^/]+/, "");
    const method = (init?.method ?? "GET").toUpperCase();

    const route = routes[`${method} ${path}`] ?? routes[path];
    if (!route) {
      return new Response(JSON.stringify({ title: "Not mocked" }), {
        status: 404,
        headers: { "Content-Type": "application/json" },
      });
    }

    const status = route.status ?? 200;
    // 204/205/304 must have a null body per the Fetch spec, so default to null.
    const body = route.json === undefined ? null : JSON.stringify(route.json);
    return new Response(body, {
      status,
      headers: { "Content-Type": "application/json" },
    });
  });

  vi.stubGlobal("fetch", mock);
  return mock;
}
