import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { PageDetail } from "../api/types";
import { ShareDialog } from "./ShareDialog";
import { installFetchMock } from "../test/fetchMock";

afterEach(() => vi.unstubAllGlobals());

const page: PageDetail = {
  id: "p1",
  workspaceId: "w1",
  parentId: null,
  title: "Roadmap",
  icon: "🚀",
  position: 0,
  visibility: "Workspace",
  permission: "View",
  publicSlug: null,
  isFavorite: false,
  version: "v0",
  createdAt: "",
  updatedAt: "",
  breadcrumb: [],
};

describe("ShareDialog", () => {
  it("makes a page public and reveals its link", async () => {
    const fetchMock = installFetchMock({
      "PUT /api/pages/p1/share": {
        json: { visibility: "Public", permission: "View", publicSlug: "abc123" },
      },
    });

    render(<ShareDialog page={page} onChanged={vi.fn()} onClose={vi.fn()} />);

    await userEvent.selectOptions(screen.getByLabelText("Visibility"), "Public");

    const putCall = fetchMock.mock.calls.find(([, init]) => init?.method === "PUT");
    expect(putCall).toBeDefined();
    expect(putCall![1]?.body).toContain("Public");
    expect(await screen.findByText(/abc123/)).toBeInTheDocument();
  });
});
