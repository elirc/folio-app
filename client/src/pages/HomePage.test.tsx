import { afterEach, describe, expect, it, vi } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import { HomePage } from "./HomePage";
import { renderWithRouter } from "../test/renderWithRouter";
import { installFetchMock } from "../test/fetchMock";

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("HomePage", () => {
  it("shows the API health status once loaded", async () => {
    installFetchMock({ "/health": { json: { status: "ok" } } });

    renderWithRouter(<HomePage />);

    await waitFor(() => {
      expect(screen.getByTestId("health-status")).toHaveTextContent("ok");
    });
  });

  it("shows 'unreachable' when the health check fails", async () => {
    installFetchMock({ "/health": { status: 500, json: { title: "boom" } } });

    renderWithRouter(<HomePage />);

    await waitFor(() => {
      expect(screen.getByTestId("health-status")).toHaveTextContent("unreachable");
    });
  });
});
