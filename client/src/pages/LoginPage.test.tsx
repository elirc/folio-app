import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { App } from "../App";
import { AuthProvider } from "../auth/AuthContext";
import { installFetchMock } from "../test/fetchMock";
import { clearSession } from "../test/authTestUtils";

afterEach(() => {
  vi.unstubAllGlobals();
  clearSession();
});

function renderApp() {
  return render(
    <MemoryRouter initialEntries={["/login"]}>
      <AuthProvider>
        <App />
      </AuthProvider>
    </MemoryRouter>,
  );
}

describe("LoginPage", () => {
  it("signs in and lands on the home page", async () => {
    const fetchMock = installFetchMock({
      "POST /api/auth/login": {
        json: {
          token: "jwt-123",
          expiresAt: "",
          member: { id: "m1", workspaceId: "w1", name: "Ada", email: "ada@acme.test", role: "Owner" },
        },
      },
      "/health": { json: { status: "ok" } },
      "/api/workspaces": { json: [] },
    });

    renderApp();

    await userEvent.type(screen.getByLabelText("Email"), "ada@acme.test");
    await userEvent.type(screen.getByLabelText("Password"), "password");
    await userEvent.click(screen.getByRole("button", { name: "Sign in" }));

    // Landed on the authenticated home page.
    expect(await screen.findByRole("heading", { name: "Folio" })).toBeInTheDocument();

    const loginCall = fetchMock.mock.calls.find(([, init]) => init?.method === "POST");
    expect(loginCall).toBeDefined();
    expect(loginCall![1]?.body).toContain("ada@acme.test");
  });

  it("shows an error on invalid credentials", async () => {
    installFetchMock({
      "POST /api/auth/login": { status: 401, json: { detail: "Invalid email or password." } },
    });

    renderApp();

    await userEvent.type(screen.getByLabelText("Email"), "ada@acme.test");
    await userEvent.type(screen.getByLabelText("Password"), "wrong");
    await userEvent.click(screen.getByRole("button", { name: "Sign in" }));

    await waitFor(() => {
      expect(screen.getByRole("alert")).toHaveTextContent("Invalid email or password.");
    });
  });
});
