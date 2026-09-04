import { apiRequest } from "./client";
import type { LoginResponse } from "../types/auth";

export function loginRequest(email: string, password: string) {
  return apiRequest<LoginResponse>("/api/auth/login", {
    method: "POST",
    body: JSON.stringify({ email, password }),
  });
}
