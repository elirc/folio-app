import { useState } from "react";
import { useNavigate } from "react-router-dom";
import type { Notification } from "../api/types";
import { getNotifications, markAllNotificationsRead, markNotificationRead } from "../api/folio";
import { useAuth } from "../auth/AuthContext";
import { useAsync } from "../hooks/useAsync";

export function NotificationInbox() {
  const { member } = useAuth();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const { data, reload } = useAsync<Notification[]>((signal) => getNotifications(signal), []);

  const notifications = data ?? [];
  const unread = notifications.filter((n) => !n.isRead).length;

  async function openNotification(n: Notification) {
    if (!n.isRead) {
      await markNotificationRead(n.id);
    }
    setOpen(false);
    reload();
    if (n.pageId && member) {
      navigate(`/w/${member.workspaceId}/p/${n.pageId}`);
    }
  }

  async function markAll() {
    await markAllNotificationsRead();
    reload();
  }

  return (
    <div className="notif">
      <button
        type="button"
        className="notif-bell"
        aria-label={`Notifications${unread > 0 ? ` (${unread} unread)` : ""}`}
        onClick={() => setOpen((v) => !v)}
      >
        🔔
        {unread > 0 && <span className="notif-badge">{unread}</span>}
      </button>

      {open && (
        <div className="notif-panel" role="dialog" aria-label="Notifications">
          <div className="notif-header">
            <span className="sidebar-title">Notifications</span>
            {unread > 0 && (
              <button type="button" className="link-btn" onClick={markAll}>
                Mark all read
              </button>
            )}
          </div>

          {notifications.length === 0 ? (
            <p className="muted notif-empty">You're all caught up.</p>
          ) : (
            <ul className="notif-list">
              {notifications.map((n) => (
                <li key={n.id}>
                  <button
                    type="button"
                    className={`notif-item${n.isRead ? "" : " unread"}`}
                    onClick={() => openNotification(n)}
                  >
                    <span className="notif-summary">{n.summary}</span>
                    {n.pageTitle && <span className="muted notif-page">{n.pageTitle}</span>}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}
    </div>
  );
}
