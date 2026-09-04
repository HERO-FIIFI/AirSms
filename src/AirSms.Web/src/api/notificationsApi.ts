import { apiRequest, notificationsApiBaseUrl } from "./client";
import type {
  Notification,
  UnreadNotificationCountResponse,
} from "../types/notifications";

export function listNotifications() {
  return apiRequest<Notification[]>("/api/notifications", {}, notificationsApiBaseUrl);
}

export function getUnreadNotificationCount() {
  return apiRequest<UnreadNotificationCountResponse>(
    "/api/notifications/unread-count",
    {},
    notificationsApiBaseUrl);
}

export function markNotificationRead(id: string) {
  return apiRequest<Notification>(
    `/api/notifications/${id}/read`,
    { method: "POST" },
    notificationsApiBaseUrl);
}
