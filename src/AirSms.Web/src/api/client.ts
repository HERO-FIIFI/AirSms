export type ProblemDetails = {
  title?: string;
  detail?: string;
  status?: number;
};

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly problem?: ProblemDetails,
  ) {
    super(problem?.detail || problem?.title || `Request failed with ${status}`);
  }
}

declare global {
  interface Window {
    __AIRSMS_CONFIG__?: {
      apiBaseUrl?: string;
      notificationsApiBaseUrl?: string;
    };
  }
}

const runtimeConfig = typeof window === "undefined"
  ? undefined
  : window.__AIRSMS_CONFIG__;

const apiBaseUrl =
  runtimeConfig?.apiBaseUrl ||
  import.meta.env.VITE_API_BASE_URL ||
  "http://localhost:5000";
export const notificationsApiBaseUrl =
  runtimeConfig?.notificationsApiBaseUrl ||
  import.meta.env.VITE_NOTIFICATIONS_API_BASE_URL ||
  apiBaseUrl;

let accessToken: string | null = null;
let unauthorizedHandler: (() => void) | null = null;

export function setAccessToken(token: string | null) {
  accessToken = token;
}

export function onUnauthorized(handler: () => void) {
  unauthorizedHandler = handler;
}

export async function apiRequest<T>(
  path: string,
  options: RequestInit = {},
  baseUrl = apiBaseUrl,
): Promise<T> {
  const headers = new Headers(options.headers);
  headers.set("Accept", "application/json");

  if (options.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  if (accessToken) {
    headers.set("Authorization", `Bearer ${accessToken}`);
  }

  let response: Response;
  try {
    response = await fetch(`${baseUrl}${path}`, {
      ...options,
      headers,
    });
  } catch {
    throw new Error("Unable to reach the AirSms API.");
  }

  if (response.status === 401) {
    unauthorizedHandler?.();
  }

  if (!response.ok) {
    throw new ApiError(response.status, await readProblem(response));
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json() as Promise<T>;
}

async function readProblem(response: Response): Promise<ProblemDetails | undefined> {
  const contentType = response.headers.get("content-type") || "";
  if (!contentType.includes("json")) {
    return undefined;
  }

  return response.json() as Promise<ProblemDetails>;
}
