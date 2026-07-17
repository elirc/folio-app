import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HistoryPanel } from "./HistoryPanel";
import { installFetchMock } from "../test/fetchMock";

afterEach(() => vi.unstubAllGlobals());

const versions = [
  { versionNumber: 2, title: "Getting Started", icon: "📘", blockCount: 6, createdByName: "Ada Lovelace", label: "Before restore to v1", createdAt: "2026-01-02T00:00:00Z" },
  { versionNumber: 1, title: "Getting Started", icon: "📘", blockCount: 5, createdByName: "Ada Lovelace", label: null, createdAt: "2026-01-01T00:00:00Z" },
];

describe("HistoryPanel", () => {
  it("lists versions and shows a diff summary when a version is expanded", async () => {
    installFetchMock({
      "/api/pages/p1/versions": { json: versions },
      "/api/pages/p1/versions/1": {
        json: { versionNumber: 1, title: "Getting Started", icon: "📘", blocks: [], diff: { added: 3, removed: 1, changed: 2 }, createdByName: "Ada", label: null, createdAt: "" },
      },
    });

    render(<HistoryPanel pageId="p1" onChanged={vi.fn()} onClose={vi.fn()} />);

    expect(await screen.findByText("v1")).toBeInTheDocument();
    expect(screen.getByText("v2")).toBeInTheDocument();

    await userEvent.click(screen.getByText("v1"));

    await waitFor(() => {
      expect(screen.getByText("+3")).toBeInTheDocument();
    });
    expect(screen.getByText("−1")).toBeInTheDocument();
    expect(screen.getByText("~2")).toBeInTheDocument();
  });

  it("saves a version and restores one", async () => {
    const fetchMock = installFetchMock({
      "/api/pages/p1/versions": { json: versions },
      "POST /api/pages/p1/versions": { status: 201, json: { versionNumber: 3, title: "Getting Started", icon: null, blockCount: 5, createdByName: "Ada", label: null, createdAt: "" } },
      "POST /api/pages/p1/versions/1/restore": { json: { versionNumber: 4, title: "Getting Started", icon: null, blockCount: 6, createdByName: "Ada", label: "Before restore to v1", createdAt: "" } },
    });
    const onChanged = vi.fn();

    render(<HistoryPanel pageId="p1" onChanged={onChanged} onClose={vi.fn()} />);
    await screen.findByText("v1");

    await userEvent.click(screen.getByRole("button", { name: "Save version" }));
    await waitFor(() => {
      expect(fetchMock.mock.calls.some(([url, init]) => String(url).endsWith("/versions") && init?.method === "POST")).toBe(true);
    });

    await userEvent.click(screen.getAllByRole("button", { name: "Restore" })[1]);
    await waitFor(() => {
      const restoreCall = fetchMock.mock.calls.find(
        ([url, init]) => String(url).includes("/versions/1/restore") && init?.method === "POST",
      );
      expect(restoreCall).toBeDefined();
    });
    expect(onChanged).toHaveBeenCalled();
  });
});
