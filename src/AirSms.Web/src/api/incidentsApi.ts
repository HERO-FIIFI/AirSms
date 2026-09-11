import { apiRequest } from "./client";
import type {
  CreateIncidentRequest,
  Incident,
  ListIncidentFilters,
} from "../types/incidents";

export function listIncidents(filters: ListIncidentFilters) {
  const params = new URLSearchParams({
    page: String(filters.page),
    pageSize: String(filters.pageSize),
  });

  if (filters.status) {
    params.set("status", filters.status);
  }

  if (filters.severity) {
    params.set("severity", filters.severity);
  }

  if (filters.category) {
    params.set("category", filters.category);
  }

  return apiRequest<Incident[]>(`/api/incidents?${params}`);
}

export function getIncident(id: string) {
  return apiRequest<Incident>(`/api/incidents/${id}`);
}

export function createIncident(request: CreateIncidentRequest) {
  return apiRequest<Incident>("/api/incidents", {
    method: "POST",
    body: JSON.stringify(request),
  });
}

export function assignIncident(id: string, assignedToUserId: string) {
  return apiRequest<Incident>(`/api/incidents/${id}/assign`, {
    method: "POST",
    body: JSON.stringify({ assignedToUserId }),
  });
}

export function startIncident(id: string) {
  return apiRequest<Incident>(`/api/incidents/${id}/start`, { method: "POST" });
}

export function resolveIncident(id: string) {
  return apiRequest<Incident>(`/api/incidents/${id}/resolve`, { method: "POST" });
}

export function closeIncident(id: string) {
  return apiRequest<Incident>(`/api/incidents/${id}/close`, { method: "POST" });
}
