import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { App } from "./App";
import { AuthProvider } from "./auth/AuthContext";
import { installFetchMock } from "./test/fetchMock";
import { seedSession, clearSession } from "./test/authTestUtils";

afterEach(() => {
  vi.unstubAllGlobals();
  clearSession();
});

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <AuthProvider>
        <App />
      </AuthProvider>
    </MemoryRouter>,
  );
}

describe("App routing", () => {
  it("redirects to the login page when not authenticated", async () => {
    installFetchMock({});

    renderAt("/");

    expect(await screen.findByRole("heading", { name: "Sign in to Folio" })).toBeInTheDocument();
  });

  it("renders the home page at / when authenticated", async () => {
    seedSession();
    installFetchMock({ "/health": { json: { status: "ok" } }, "/api/workspaces": { json: [] } });

    renderAt("/");

    expect(await screen.findByRole("heading", { name: "Folio" })).toBeInTheDocument();
  });

  it("renders the not-found page for an unknown route when authenticated", async () => {
    seedSession();
    installFetchMock({});

    renderAt("/does-not-exist");

    expect(await screen.findByRole("heading", { name: "Not found" })).toBeInTheDocument();
  });
});
