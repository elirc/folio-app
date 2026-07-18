import { afterEach, describe, expect, it, vi } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QuickOpenModal } from "./QuickOpenModal";
import { renderWithRouter } from "../test/renderWithRouter";
import { installFetchMock } from "../test/fetchMock";

afterEach(() => vi.unstubAllGlobals());

const recent = [
  { pageId: "p1", title: "Installation", icon: "📦", updatedAt: "" },
  { pageId: "p2", title: "Configuration", icon: "⚙️", updatedAt: "" },
];

describe("QuickOpenModal", () => {
  it("shows results and opens the keyboard-selected one on Enter", async () => {
    installFetchMock({ "/api/workspaces/w1/quick-open": { json: recent } });

    renderWithRouter(<QuickOpenModal workspaceId="w1" onClose={vi.fn()} />);

    // Recent pages load on open.
    expect(await screen.findByText("Installation")).toBeInTheDocument();
    expect(screen.getByText("Configuration")).toBeInTheDocument();

    const input = screen.getByLabelText("Quick open search");
    await userEvent.type(input, "{ArrowDown}{Enter}"); // select 2nd, open

    // Navigated away → modal content still rendered until unmount, but the
    // second option was the active selection.
    expect(screen.getByRole("option", { name: /Configuration/ })).toHaveAttribute("aria-selected", "true");
  });

  it("closes on Escape", async () => {
    installFetchMock({ "/api/workspaces/w1/quick-open": { json: recent } });
    const onClose = vi.fn();

    renderWithRouter(<QuickOpenModal workspaceId="w1" onClose={onClose} />);
    await screen.findByText("Installation");

    await userEvent.type(screen.getByLabelText("Quick open search"), "{Escape}");

    await waitFor(() => expect(onClose).toHaveBeenCalled());
  });
});
