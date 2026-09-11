import { apiRequest } from "./client";
import type { User, UserRole } from "../types/auth";

export function listUsers() {
  return apiRequest<User[]>("/api/users");
}

export function changeUserRole(id: string, role: UserRole) {
  return apiRequest<User>(`/api/users/${id}/role`, {
    method: "PUT",
    body: JSON.stringify({ role }),
  });
}

export function setUserActive(id: string, isActive: boolean) {
  return apiRequest<User>(`/api/users/${id}/active`, {
    method: "PUT",
    body: JSON.stringify({ isActive }),
  });
}

export function resetUserPassword(id: string, password: string) {
  return apiRequest<User>(`/api/users/${id}/reset-password`, {
    method: "POST",
    body: JSON.stringify({ password }),
  });
}
