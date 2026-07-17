import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Block } from "../api/types";
import { BlockList } from "./BlockList";
import { installFetchMock } from "../test/fetchMock";

afterEach(() => vi.unstubAllGlobals());

const blocks: Block[] = [
  { id: "b1", pageId: "pg", type: "Heading", position: 0, content: { text: "Welcome", level: 1 }, createdAt: "", updatedAt: "" },
  { id: "b2", pageId: "pg", type: "Todo", position: 1, content: { text: "Task", checked: false }, createdAt: "", updatedAt: "" },
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
        json: { id: "b3", pageId: "pg", type: "Todo", position: 2, content: { text: "" }, createdAt: "", updatedAt: "" },
      },
    });

    render(<BlockList pageId="pg" />);
    await screen.findByDisplayValue("Welcome");

    await userEvent.click(screen.getByRole("button", { name: "+ To-do" }));

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
});
