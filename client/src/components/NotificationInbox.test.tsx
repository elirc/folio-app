import { afterEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { NotificationInbox } from "./NotificationInbox";
import { AuthProvider } from "../auth/AuthContext";
import { installFetchMock } from "../test/fetchMock";
import { seedSession, clearSession } from "../test/authTestUtils";

afterEach(() => {
  vi.unstubAllGlobals();
  clearSession();
});

const notifications = [
  { id: "n1", type: "CommentCreated", pageId: "p1", pageTitle: "Getting Started", summary: "commented on \"Getting Started\"", isRead: false, createdAt: "" },
  { id: "n2", type: "CommentCreated", pageId: "p2", pageTitle: "Engineering", summary: "commented on \"Engineering\"", isRead: true, createdAt: "" },
];

function renderInbox() {
  seedSession();
  return render(
    <MemoryRouter>
      <AuthProvider>
        <NotificationInbox />
      </AuthProvider>
    </MemoryRouter>,
  );
}

describe("NotificationInbox", () => {
  it("shows the unread badge and lists notifications on open", async () => {
    installFetchMock({ "/api/notifications": { json: notifications } });

    renderInbox();

    // One unread → badge shows 1.
    expect(await screen.findByLabelText("Notifications (1 unread)")).toBeInTheDocument();

    await userEvent.click(screen.getByLabelText("Notifications (1 unread)"));
    expect(await screen.findByText('commented on "Getting Started"')).toBeInTheDocument();
  });

  it("marks all notifications read", async () => {
    const fetchMock = installFetchMock({
      "/api/notifications": { json: notifications },
      "POST /api/notifications/read-all": { json: { count: 1 } },
    });

    renderInbox();
    await userEvent.click(await screen.findByLabelText("Notifications (1 unread)"));

    await userEvent.click(screen.getByRole("button", { name: "Mark all read" }));

    await waitFor(() => {
      const call = fetchMock.mock.calls.find(
        ([url, init]) => String(url).endsWith("/read-all") && init?.method === "POST",
      );
      expect(call).toBeDefined();
    });
  });
});
