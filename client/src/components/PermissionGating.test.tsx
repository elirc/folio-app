import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import type { Block } from "../api/types";
import { PageView } from "./PageView";
import { BlockList } from "./BlockList";
import { renderWithRouter } from "../test/renderWithRouter";
import { installFetchMock } from "../test/fetchMock";

afterEach(() => vi.unstubAllGlobals());

const pageDetail = {
  id: "p2",
  workspaceId: "w1",
  parentId: null,
  title: "Installation",
  icon: "📦",
  position: 0,
  visibility: "Workspace",
  permission: "View",
  publicSlug: null,
  isFavorite: false,
  version: "v1",
  createdAt: "",
  updatedAt: "",
  breadcrumb: [{ id: "p2", title: "Installation", icon: "📦" }],
};

const blocks: Block[] = [
  { id: "b1", pageId: "p2", parentBlockId: null, type: "Heading", position: 0, content: { text: "Welcome", level: 1 }, version: "v0", createdAt: "", updatedAt: "" },
  { id: "b2", pageId: "p2", parentBlockId: null, type: "Todo", position: 1, content: { text: "Task", checked: false }, version: "v0", createdAt: "", updatedAt: "" },
];

function mockPage() {
  installFetchMock({
    "/api/pages/p2": { json: pageDetail },
    "/api/pages/p2/blocks": { json: blocks },
    "/api/workspaces/w1/pages/tree": { json: [] },
  });
}

describe("PageView permission gating", () => {
  it("hides write-only affordances when canEdit is false (viewer)", async () => {
    mockPage();
    renderWithRouter(<PageView pageId="p2" workspaceId="w1" onChanged={vi.fn()} canEdit={false} />);

    // Wait until the nested block list has loaded (its content renders) so the
    // absence assertions reflect the loaded, read-only state.
    await screen.findByDisplayValue("Installation");
    await screen.findByDisplayValue("Welcome");

    // Write-only toolbar actions are gone…
    expect(screen.queryByRole("button", { name: "Share" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Duplicate" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Save as template" })).toBeNull();
    // …as is the block-insert toolbar.
    expect(screen.queryByRole("toolbar", { name: "Add block" })).toBeNull();

    // Read-only affordances remain available.
    expect(screen.getByRole("button", { name: "Comments" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "History" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Export" })).toBeInTheDocument();
  });

  it("shows write affordances by default (editor/owner)", async () => {
    mockPage();
    renderWithRouter(<PageView pageId="p2" workspaceId="w1" onChanged={vi.fn()} />);

    await screen.findByDisplayValue("Installation");
    // The add-block toolbar appears once the nested block list has loaded.
    expect(await screen.findByRole("toolbar", { name: "Add block" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Share" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Duplicate" })).toBeInTheDocument();
  });
});

describe("BlockList permission gating", () => {
  it("renders read-only blocks with no editing controls when canEdit is false", async () => {
    installFetchMock({
      "/api/pages/pg/blocks": { json: blocks },
      "/api/workspaces/w1/pages/tree": { json: [] },
    });

    render(<BlockList pageId="pg" workspaceId="w1" canEdit={false} />);

    // Content is still shown…
    expect(await screen.findByDisplayValue("Welcome")).toBeInTheDocument();
    // …but the add bar and per-block gutter/controls are hidden.
    expect(screen.queryByRole("toolbar", { name: "Add block" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Delete block" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Move block up" })).toBeNull();
    // The text is read-only and the to-do checkbox can't be toggled.
    expect(screen.getByDisplayValue("Welcome")).toHaveAttribute("readonly");
    expect(screen.getByLabelText("Toggle to-do")).toBeDisabled();
  });

  it("renders editing controls by default", async () => {
    installFetchMock({
      "/api/pages/pg/blocks": { json: blocks },
      "/api/workspaces/w1/pages/tree": { json: [] },
    });

    render(<BlockList pageId="pg" workspaceId="w1" />);

    await screen.findByDisplayValue("Welcome");
    expect(screen.getByRole("toolbar", { name: "Add block" })).toBeInTheDocument();
    expect(screen.getAllByRole("button", { name: "Delete block" }).length).toBeGreaterThan(0);
  });
});
