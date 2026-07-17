import { afterEach, describe, expect, it, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { PageTreeNode } from "../api/types";
import { Sidebar } from "./Sidebar";
import { renderWithRouter } from "../test/renderWithRouter";
import { installFetchMock } from "../test/fetchMock";

afterEach(() => vi.unstubAllGlobals());

const tree: PageTreeNode[] = [
  {
    id: "p1",
    parentId: null,
    title: "Getting Started",
    icon: "📘",
    position: 0,
    children: [
      { id: "p2", parentId: "p1", title: "Installation", icon: null, position: 0, children: [] },
    ],
  },
];

describe("Sidebar", () => {
  it("renders the nested page tree", () => {
    installFetchMock({});
    renderWithRouter(<Sidebar workspaceId="w1" tree={tree} onChanged={vi.fn()} />);

    expect(screen.getByText("Getting Started")).toBeInTheDocument();
    expect(screen.getByText("Installation")).toBeInTheDocument();
  });

  it("creates a root page when clicking New page", async () => {
    const fetchMock = installFetchMock({
      "POST /api/workspaces/w1/pages": { status: 201, json: { id: "new", title: "Untitled" } },
    });
    const onChanged = vi.fn();
    renderWithRouter(<Sidebar workspaceId="w1" tree={tree} onChanged={onChanged} />);

    await userEvent.click(screen.getByRole("button", { name: "New page" }));

    expect(fetchMock).toHaveBeenCalled();
    const [, init] = fetchMock.mock.calls.at(-1)!;
    expect(init?.method).toBe("POST");
    expect(onChanged).toHaveBeenCalled();
  });

  it("deletes a page when clicking the delete action", async () => {
    const fetchMock = installFetchMock({
      "DELETE /api/pages/p1": { status: 204 },
    });
    renderWithRouter(<Sidebar workspaceId="w1" tree={tree} onChanged={vi.fn()} />);

    await userEvent.click(screen.getByRole("button", { name: "Delete Getting Started" }));

    const call = fetchMock.mock.calls.find(([, init]) => init?.method === "DELETE");
    expect(call).toBeDefined();
  });
});
