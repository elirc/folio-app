import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { App } from "./App";
import { installFetchMock } from "./test/fetchMock";

afterEach(() => vi.unstubAllGlobals());

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>,
  );
}

describe("App routing", () => {
  it("renders the home page at /", async () => {
    installFetchMock({ "/health": { json: { status: "ok" } }, "/api/workspaces": { json: [] } });

    renderAt("/");

    expect(await screen.findByRole("heading", { name: "Folio" })).toBeInTheDocument();
  });

  it("renders the not-found page for an unknown route", async () => {
    installFetchMock({});

    renderAt("/does-not-exist");

    expect(await screen.findByRole("heading", { name: "Not found" })).toBeInTheDocument();
  });
});
