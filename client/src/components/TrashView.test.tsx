import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { TrashView } from "./TrashView";
import { installFetchMock } from "../test/fetchMock";

afterEach(() => vi.unstubAllGlobals());

describe("TrashView", () => {
  it("lists trashed pages and restores one", async () => {
    const fetchMock = installFetchMock({
      "/api/workspaces/w1/trash": {
        json: [{ id: "p1", title: "Old Notes", icon: "📄", deletedAt: "2026-01-01T00:00:00Z" }],
      },
      "POST /api/pages/p1/restore": { json: { id: "p1" } },
    });

    render(<TrashView workspaceId="w1" onChanged={vi.fn()} />);

    expect(await screen.findByText(/Old Notes/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Restore" }));

    const restoreCall = fetchMock.mock.calls.find(
      ([url, init]) => String(url).includes("/restore") && init?.method === "POST",
    );
    expect(restoreCall).toBeDefined();
  });

  it("shows an empty state when trash is empty", async () => {
    installFetchMock({ "/api/workspaces/w1/trash": { json: [] } });

    render(<TrashView workspaceId="w1" onChanged={vi.fn()} />);

    expect(await screen.findByText("Trash is empty.")).toBeInTheDocument();
  });
});
