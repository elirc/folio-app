import { afterEach, describe, expect, it, vi } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { PageView } from "./PageView";
import { renderWithRouter } from "../test/renderWithRouter";
import { installFetchMock } from "../test/fetchMock";

afterEach(() => vi.unstubAllGlobals());

const pageDetail = {
  id: "p2",
  workspaceId: "w1",
  parentId: "p1",
  title: "Installation",
  icon: "📦",
  position: 0,
  visibility: "Workspace",
  permission: "View",
  publicSlug: null,
  isFavorite: false,
  createdAt: "",
  updatedAt: "",
  breadcrumb: [
    { id: "p1", title: "Getting Started", icon: "📘" },
    { id: "p2", title: "Installation", icon: "📦" },
  ],
};

describe("PageView", () => {
  it("renders breadcrumb and editable title", async () => {
    installFetchMock({ "/api/pages/p2": { json: pageDetail }, "/api/pages/p2/blocks": { json: [] } });
    renderWithRouter(<PageView pageId="p2" workspaceId="w1" onChanged={vi.fn()} />);

    expect(await screen.findByDisplayValue("Installation")).toBeInTheDocument();
    expect(screen.getByText("Getting Started")).toBeInTheDocument();
  });

  it("saves a renamed title on blur", async () => {
    const fetchMock = installFetchMock({
      "/api/pages/p2": { json: pageDetail },
    });
    const onChanged = vi.fn();
    renderWithRouter(<PageView pageId="p2" workspaceId="w1" onChanged={onChanged} />);

    const input = await screen.findByLabelText("Page title");
    await userEvent.clear(input);
    await userEvent.type(input, "Setup");
    await userEvent.tab(); // blur

    await waitFor(() => {
      const putCall = fetchMock.mock.calls.find(([, init]) => init?.method === "PUT");
      expect(putCall).toBeDefined();
      expect(putCall![1]?.body).toContain("Setup");
    });
    expect(onChanged).toHaveBeenCalled();
  });
});
