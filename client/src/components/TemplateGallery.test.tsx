import { afterEach, describe, expect, it, vi } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { TemplateGallery } from "./TemplateGallery";
import { renderWithRouter } from "../test/renderWithRouter";
import { installFetchMock } from "../test/fetchMock";

afterEach(() => vi.unstubAllGlobals());

const templates = [
  {
    id: "t1", workspaceId: "w1", name: "Onboarding", description: "Starter",
    sourceTitle: "Getting Started", sourceIcon: "📘", blockCount: 5,
    createdByName: "Ada Lovelace", createdAt: "",
  },
];

describe("TemplateGallery", () => {
  it("lists templates and instantiates one", async () => {
    const fetchMock = installFetchMock({
      "/api/workspaces/w1/templates": { json: templates },
      "POST /api/workspaces/w1/templates/t1/instantiate": {
        status: 201,
        json: { id: "newpage", title: "Getting Started" },
      },
    });

    renderWithRouter(<TemplateGallery workspaceId="w1" onChanged={vi.fn()} />);

    expect(await screen.findByText(/Onboarding/)).toBeInTheDocument();
    expect(screen.getByText(/5 blocks/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Use template" }));

    await waitFor(() => {
      const call = fetchMock.mock.calls.find(
        ([url, init]) => String(url).includes("/instantiate") && init?.method === "POST",
      );
      expect(call).toBeDefined();
    });
  });

  it("shows an empty state when there are no templates", async () => {
    installFetchMock({ "/api/workspaces/w1/templates": { json: [] } });

    renderWithRouter(<TemplateGallery workspaceId="w1" onChanged={vi.fn()} />);

    expect(await screen.findByText(/No templates yet/)).toBeInTheDocument();
  });
});
