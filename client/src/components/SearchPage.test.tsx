import { afterEach, describe, expect, it, vi } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { SearchPage } from "./SearchPage";
import { renderWithRouter } from "../test/renderWithRouter";
import { installFetchMock } from "../test/fetchMock";

afterEach(() => vi.unstubAllGlobals());

const members = [
  { id: "m1", workspaceId: "w1", name: "Ada Lovelace", email: "ada@acme.test", role: "Owner" },
];

describe("SearchPage", () => {
  it("runs a filtered search and lists results", async () => {
    const fetchMock = installFetchMock({
      "/api/workspaces/w1/members": { json: members },
      "/api/workspaces/w1/search": {
        json: [
          { pageId: "p1", title: "Installation", icon: "📦", matchedTitle: true, snippet: null, updatedAt: "" },
        ],
      },
    });

    renderWithRouter(<SearchPage workspaceId="w1" />);

    await userEvent.type(screen.getByLabelText("Search query"), "install");

    expect(await screen.findByText(/Installation/)).toBeInTheDocument();

    await waitFor(() => {
      const call = fetchMock.mock.calls.find(([url]) => String(url).includes("/search?"));
      expect(call).toBeDefined();
      expect(String(call![0])).toContain("q=install");
    });
  });

  it("includes the favorites filter in the request", async () => {
    const fetchMock = installFetchMock({
      "/api/workspaces/w1/members": { json: members },
      "/api/workspaces/w1/search": { json: [] },
    });

    renderWithRouter(<SearchPage workspaceId="w1" />);

    await userEvent.click(screen.getByLabelText("Only favorites"));

    await waitFor(() => {
      const call = fetchMock.mock.calls.find(([url]) => String(url).includes("favorites=true"));
      expect(call).toBeDefined();
    });
  });
});
