import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Comment, Member } from "../api/types";
import { CommentSidebar, renderCommentBody } from "./CommentSidebar";
import { installFetchMock } from "../test/fetchMock";

afterEach(() => vi.unstubAllGlobals());

const members: Member[] = [
  { id: "m-grace", workspaceId: "w1", name: "Grace Hopper", email: "grace@acme.test", role: "Editor" },
];

const comments: Comment[] = [
  {
    id: "c1", pageId: "p1", blockId: null, parentCommentId: null,
    authorMemberId: "m-ada", authorName: "Ada Lovelace",
    body: "Hi @[Grace Hopper](m-grace)", isResolved: false, resolvedAt: null,
    mentions: [{ memberId: "m-grace", name: "Grace Hopper" }],
    createdAt: "", updatedAt: "",
  },
];

describe("renderCommentBody", () => {
  it("renders mention tokens as @Name", () => {
    expect(renderCommentBody("cc @[Grace Hopper](11111111-1111-1111-1111-111111111111)!"))
      .toBe("cc @Grace Hopper!");
  });
});

describe("CommentSidebar", () => {
  it("lists comments and posts a new one with a mention", async () => {
    const fetchMock = installFetchMock({
      "/api/pages/p1/comments": { json: comments },
      "/api/workspaces/w1/members": { json: members },
      "POST /api/pages/p1/comments": {
        status: 201,
        json: { ...comments[0], id: "c2", body: "New comment" },
      },
    });

    render(<CommentSidebar pageId="p1" workspaceId="w1" onClose={vi.fn()} />);

    // Existing comment shows with the mention rendered.
    expect(await screen.findByText("Hi @Grace Hopper")).toBeInTheDocument();

    // Compose a mention via the picker, then submit.
    await userEvent.selectOptions(screen.getByLabelText("Mention a member"), "m-grace");
    const composer = screen.getByLabelText("Comment on this page…");
    expect((composer as HTMLTextAreaElement).value).toContain("@[Grace Hopper](m-grace)");

    await userEvent.type(composer, "please review");
    await userEvent.click(screen.getByRole("button", { name: "Comment" }));

    await waitFor(() => {
      const post = fetchMock.mock.calls.find(
        ([url, init]) => String(url).endsWith("/comments") && init?.method === "POST",
      );
      expect(post).toBeDefined();
      expect(post![1]?.body).toContain("@[Grace Hopper](m-grace)");
    });
  });

  it("resolves a comment thread", async () => {
    const fetchMock = installFetchMock({
      "/api/pages/p1/comments": { json: comments },
      "/api/workspaces/w1/members": { json: members },
      "POST /api/comments/c1/resolve": { json: { ...comments[0], isResolved: true } },
    });

    render(<CommentSidebar pageId="p1" workspaceId="w1" onClose={vi.fn()} />);
    await screen.findByText("Hi @Grace Hopper");

    await userEvent.click(screen.getByRole("button", { name: "Resolve" }));

    await waitFor(() => {
      const call = fetchMock.mock.calls.find(
        ([url, init]) => String(url).includes("/resolve") && init?.method === "POST",
      );
      expect(call).toBeDefined();
    });
  });
});
