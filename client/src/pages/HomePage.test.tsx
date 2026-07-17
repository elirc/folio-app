import { afterEach, describe, expect, it, vi } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import { HomePage } from "./HomePage";
import { renderWithRouter } from "../test/renderWithRouter";
import { installFetchMock } from "../test/fetchMock";

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("HomePage", () => {
  it("shows the API health status and workspace list once loaded", async () => {
    installFetchMock({
      "/health": { json: { status: "ok" } },
      "/api/workspaces": {
        json: [
          { id: "w1", name: "Acme Docs", slug: "acme", memberCount: 3, pageCount: 7, createdAt: "" },
        ],
      },
    });

    renderWithRouter(<HomePage />);

    await waitFor(() => {
      expect(screen.getByTestId("health-status")).toHaveTextContent("ok");
    });
    expect(await screen.findByText("Acme Docs")).toBeInTheDocument();
    expect(screen.getByText(/7 pages/)).toBeInTheDocument();
  });

  it("shows 'unreachable' when the health check fails", async () => {
    installFetchMock({
      "/health": { status: 500, json: { title: "boom" } },
      "/api/workspaces": { json: [] },
    });

    renderWithRouter(<HomePage />);

    await waitFor(() => {
      expect(screen.getByTestId("health-status")).toHaveTextContent("unreachable");
    });
  });
});
