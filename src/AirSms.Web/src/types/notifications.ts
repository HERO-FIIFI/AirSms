export type Notification = {
  id: string;
  userId: string;
  type: string;
  title: string;
  message: string;
  relatedIncidentId: string | null;
  createdAt: string;
  readAt: string | null;
  sourceEventId: string;
};

export type UnreadNotificationCountResponse = {
  count: number;
};
