import { useEffect, useState } from "react";
import { ApiError } from "../api/client";
import {
  listNotifications,
  markNotificationRead,
} from "../api/notificationsApi";
import type { Notification } from "../types/notifications";
import { formatDate } from "../utils/format";

export function NotificationsPage() {
  const [notifications, setNotifications] = useState<Notification[]>([]);
  const [loading, setLoading] = useState(true);
  const [savingId, setSavingId] = useState<string | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    loadNotifications();
  }, []);

  function loadNotifications() {
    setLoading(true);
    setError("");

    listNotifications()
      .then(setNotifications)
      .catch((exception) => {
        setError(
          exception instanceof ApiError
            ? exception.problem?.detail || exception.message
            : "Unable to load notifications.",
        );
      })
      .finally(() => setLoading(false));
  }

  async function markRead(notification: Notification) {
    if (notification.readAt) {
      return;
    }

    setSavingId(notification.id);
    setError("");

    try {
      const updated = await markNotificationRead(notification.id);
      setNotifications((current) =>
        current.map((item) => (item.id === updated.id ? updated : item)),
      );
    } catch (exception) {
      setError(
        exception instanceof ApiError
          ? exception.problem?.detail || exception.message
          : "Unable to mark notification as read.",
      );
    } finally {
      setSavingId(null);
    }
  }

  const unreadCount = notifications.filter((item) => !item.readAt).length;

  return (
    <section className="page">
      <div className="page-title-row">
        <div>
          <h2>Notifications</h2>
          <p className="muted">{unreadCount} unread notification{unreadCount === 1 ? "" : "s"}</p>
        </div>
        <button className="secondary-button" onClick={loadNotifications}>
          Refresh
        </button>
      </div>

      {error && <div className="form-error">{error}</div>}

      {loading ? (
        <div className="empty-state">Loading notifications...</div>
      ) : notifications.length === 0 ? (
        <div className="empty-state">No notifications yet.</div>
      ) : (
        <div className="notification-list">
          {notifications.map((notification) => (
            <article
              className={notification.readAt ? "notification-item" : "notification-item unread"}
              key={notification.id}
            >
              <div>
                <div className="notification-title-row">
                  <h3>{notification.title}</h3>
                  {!notification.readAt && <span className="count-badge">New</span>}
                </div>
                <p>{notification.message}</p>
                <div className="notification-meta">
                  <span>{notification.type}</span>
                  <span>{formatDate(notification.createdAt)}</span>
                  {notification.relatedIncidentId && (
                    <span className="mono">{notification.relatedIncidentId}</span>
                  )}
                </div>
              </div>
              <button
                className="secondary-button"
                disabled={Boolean(notification.readAt) || savingId === notification.id}
                onClick={() => markRead(notification)}
              >
                {notification.readAt ? "Read" : "Mark read"}
              </button>
            </article>
          ))}
        </div>
      )}
    </section>
  );
}
