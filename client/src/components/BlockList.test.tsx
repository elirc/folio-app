import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Block } from "../api/types";
import { BlockList } from "./BlockList";
import { installFetchMock } from "../test/fetchMock";

afterEach(() => vi.unstubAllGlobals());

const blocks: Block[] = [
  { id: "b1", pageId: "pg", parentBlockId: null, type: "Heading", position: 0, content: { text: "Welcome", level: 1 }, createdAt: "", updatedAt: "" },
  { id: "b2", pageId: "pg", parentBlockId: null, type: "Todo", position: 1, content: { text: "Task", checked: false }, createdAt: "", updatedAt: "" },
];

describe("BlockList", () => {
  it("renders each block's text", async () => {
    installFetchMock({ "/api/pages/pg/blocks": { json: blocks } });

    render(<BlockList pageId="pg" />);

    expect(await screen.findByDisplayValue("Welcome")).toBeInTheDocument();
    expect(screen.getByDisplayValue("Task")).toBeInTheDocument();
  });

  it("creates a block from the add bar", async () => {
    const fetchMock = installFetchMock({
      "/api/pages/pg/blocks": { json: blocks },
      "POST /api/pages/pg/blocks": {
        status: 201,
        json: { id: "b3", pageId: "pg", parentBlockId: null, type: "Todo", position: 2, content: { text: "" }, createdAt: "", updatedAt: "" },
      },
    });

    render(<BlockList pageId="pg" />);
    await screen.findByDisplayValue("Welcome");

    // The root add bar (not a toggle's) creates a page-level block.
    const rootBar = screen.getByRole("toolbar", { name: "Add block" });
    await userEvent.click(within(rootBar).getByRole("button", { name: "+ To-do" }));

    const postCall = fetchMock.mock.calls.find(([, init]) => init?.method === "POST");
    expect(postCall).toBeDefined();
    expect(postCall![1]?.body).toContain("Todo");
  });

  it("toggles a to-do via its checkbox", async () => {
    const fetchMock = installFetchMock({
      "/api/pages/pg/blocks": { json: blocks },
      "PUT /api/blocks/b2": { json: { ...blocks[1], content: { text: "Task", checked: true } } },
    });

    render(<BlockList pageId="pg" />);
    await screen.findByDisplayValue("Task");

    await userEvent.click(screen.getByLabelText("Toggle to-do"));

    const putCall = fetchMock.mock.calls.find(([, init]) => init?.method === "PUT");
    expect(putCall).toBeDefined();
    expect(putCall![1]?.body).toContain("checked");
  });

  // ---- v2 block types + nesting ----

  it("renders v2 block types (callout, divider, image, table)", async () => {
    const v2: Block[] = [
      { id: "c1", pageId: "pg", parentBlockId: null, type: "Callout", position: 0, content: { text: "Heads up", emoji: "💡" }, createdAt: "", updatedAt: "" },
      { id: "c2", pageId: "pg", parentBlockId: null, type: "Divider", position: 1, content: {}, createdAt: "", updatedAt: "" },
      { id: "c3", pageId: "pg", parentBlockId: null, type: "Image", position: 2, content: { url: "https://x/y.png", alt: "Diagram" }, createdAt: "", updatedAt: "" },
      { id: "c4", pageId: "pg", parentBlockId: null, type: "Table", position: 3, content: { rows: [["A", "B"]] }, createdAt: "", updatedAt: "" },
    ];
    installFetchMock({ "/api/pages/pg/blocks": { json: v2 } });

    render(<BlockList pageId="pg" />);

    expect(await screen.findByDisplayValue("Heads up")).toBeInTheDocument();
    expect(screen.getByLabelText("Divider")).toBeInTheDocument();
    expect(screen.getByAltText("Diagram")).toBeInTheDocument();
    expect(screen.getByDisplayValue("A")).toBeInTheDocument();
    expect(screen.getByDisplayValue("B")).toBeInTheDocument();
  });

  it("renders toggle children and adds a child under the toggle", async () => {
    const nested: Block[] = [
      { id: "t1", pageId: "pg", parentBlockId: null, type: "Toggle", position: 0, content: { text: "More", collapsed: false }, createdAt: "", updatedAt: "" },
      { id: "t1a", pageId: "pg", parentBlockId: "t1", type: "Bulleted", position: 0, content: { text: "Nested item" }, createdAt: "", updatedAt: "" },
    ];
    const fetchMock = installFetchMock({
      "/api/pages/pg/blocks": { json: nested },
      "POST /api/pages/pg/blocks": {
        status: 201,
        json: { id: "t1b", pageId: "pg", parentBlockId: "t1", type: "Paragraph", position: 1, content: { text: "" }, createdAt: "", updatedAt: "" },
      },
    });

    render(<BlockList pageId="pg" />);

    // The nested child renders under the (expanded) toggle.
    expect(await screen.findByDisplayValue("Nested item")).toBeInTheDocument();

    // Add a child via the toggle's own add bar.
    const toggleBar = screen.getByRole("toolbar", { name: "Add block inside toggle" });
    await userEvent.click(within(toggleBar).getByRole("button", { name: "+ Text" }));

    const postCall = fetchMock.mock.calls.find(([, init]) => init?.method === "POST");
    expect(postCall).toBeDefined();
    expect(postCall![1]?.body).toContain('"parentId":"t1"');
  });
});
