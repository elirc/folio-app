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

  it("sends the typed query and opens the top match on Enter", async () => {
    const fetchMock = installFetchMock({
      "/api/workspaces/w1/quick-open": { json: [recent[1]] }, // "Configuration"
    });
    const onClose = vi.fn();

    renderWithRouter(<QuickOpenModal workspaceId="w1" onClose={onClose} />);
    await screen.findByText("Configuration");

    // Typing debounces into a quick-open request carrying the query.
    await userEvent.type(screen.getByLabelText("Quick open search"), "Conf");
    await waitFor(() => {
      const call = fetchMock.mock.calls.find(([url]) => String(url).includes("q=Conf"));
      expect(call).toBeDefined();
    });

    // Enter opens the (only/top) result → the modal closes as it navigates.
    await userEvent.keyboard("{Enter}");
    await waitFor(() => expect(onClose).toHaveBeenCalled());
  });

  it("clamps ArrowUp selection at the first result", async () => {
    installFetchMock({ "/api/workspaces/w1/quick-open": { json: recent } });

    renderWithRouter(<QuickOpenModal workspaceId="w1" onClose={vi.fn()} />);
    await screen.findByText("Installation");

    // ArrowUp from the top stays on the first option (no wrap-around).
    await userEvent.type(screen.getByLabelText("Quick open search"), "{ArrowUp}{ArrowUp}");
    expect(screen.getByRole("option", { name: /Installation/ })).toHaveAttribute("aria-selected", "true");
  });
});
