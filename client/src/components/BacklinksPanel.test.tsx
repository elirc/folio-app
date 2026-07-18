import { afterEach, describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import { BacklinksPanel } from "./BacklinksPanel";
import { renderWithRouter } from "../test/renderWithRouter";
import { installFetchMock } from "../test/fetchMock";

afterEach(() => vi.unstubAllGlobals());

describe("BacklinksPanel", () => {
  it("shows inbound backlinks and flags broken outgoing links", async () => {
    installFetchMock({
      "/api/pages/p1/backlinks": {
        json: [
          { sourcePageId: "p2", sourcePageTitle: "Engineering", sourcePageIcon: "🛠️", sourceBlockId: "b1" },
        ],
      },
      "/api/pages/p1/links": {
        json: [
          { targetPageId: "p3", targetTitle: "Architecture", isBroken: false, sourceBlockId: "b2" },
          { targetPageId: "p4", targetTitle: "Deleted Page", isBroken: true, sourceBlockId: "b3" },
        ],
      },
    });

    renderWithRouter(<BacklinksPanel pageId="p1" workspaceId="w1" onClose={vi.fn()} />);

    // Inbound backlink.
    expect(await screen.findByText("Engineering")).toBeInTheDocument();

    // Outgoing links: valid + broken.
    expect(screen.getByText("Architecture")).toBeInTheDocument();
    expect(screen.getByText(/Deleted Page \(broken\)/)).toBeInTheDocument();
  });

  it("shows an empty state when nothing links here", async () => {
    installFetchMock({
      "/api/pages/p1/backlinks": { json: [] },
      "/api/pages/p1/links": { json: [] },
    });

    renderWithRouter(<BacklinksPanel pageId="p1" workspaceId="w1" onClose={vi.fn()} />);

    expect(await screen.findByText("No pages link here yet.")).toBeInTheDocument();
  });
});
