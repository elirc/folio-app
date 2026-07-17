import { afterEach, describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { SearchBox } from "./SearchBox";
import { renderWithRouter } from "../test/renderWithRouter";
import { installFetchMock } from "../test/fetchMock";

afterEach(() => vi.unstubAllGlobals());

describe("SearchBox", () => {
  it("shows results for a query", async () => {
    installFetchMock({
      "/api/workspaces/w1/search": {
        json: [
          { pageId: "p1", title: "Installation", icon: "📦", matchedTitle: true, snippet: null },
          { pageId: "p2", title: "Config", icon: null, matchedTitle: false, snippet: "…install steps" },
        ],
      },
    });

    renderWithRouter(<SearchBox workspaceId="w1" />);
    await userEvent.type(screen.getByLabelText("Search pages"), "inst");

    expect(await screen.findByText("📦 Installation")).toBeInTheDocument();
    expect(screen.getByText("…install steps")).toBeInTheDocument();
  });
});
