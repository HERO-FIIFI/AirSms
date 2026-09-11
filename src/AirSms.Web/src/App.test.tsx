import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { App } from "./App";
import { AuthProvider } from "./auth/AuthContext";
import type { Incident } from "./types/incidents";
import type { LoginResponse, UserRole } from "./types/auth";
import type { Notification } from "./types/notifications";

function renderApp(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <AuthProvider>
        <App />
      </AuthProvider>
    </MemoryRouter>,
  );
}

function signIn(role: UserRole = "OperationsAgent") {
  sessionStorage.setItem("airsms.session", JSON.stringify(loginResponse(role)));
}

function loginResponse(role: UserRole = "OperationsAgent"): LoginResponse {
  return {
    accessToken: "test-token",
    expiresAt: new Date(Date.now() + 60000).toISOString(),
    user: {
      id: "11111111-1111-1111-1111-111111111111",
      email: "agent@airsms.test",
      firstName: "Ava",
      lastName: "Stone",
      role,
      isActive: true,
      createdAt: new Date().toISOString(),
      updatedAt: null,
    },
  };
}

function incident(overrides: Partial<Incident> = {}): Incident {
  return {
    id: "22222222-2222-2222-2222-222222222222",
    title: "Hydraulic warning",
    description: "Warning shown during inspection.",
    category: "Technical",
    severity: "High",
    status: "Open",
    flightNumber: "AS123",
    aircraftRegistration: "N123AS",
    reportedByUserId: "11111111-1111-1111-1111-111111111111",
    assignedToUserId: null,
    reportedAt: new Date().toISOString(),
    resolvedAt: null,
    createdAt: new Date().toISOString(),
    updatedAt: null,
    ...overrides,
  };
}

function notification(overrides: Partial<Notification> = {}): Notification {
  return {
    id: "33333333-3333-3333-3333-333333333333",
    userId: "11111111-1111-1111-1111-111111111111",
    type: "IncidentAssigned",
    title: "Incident assigned",
    message: "Incident 22222222-2222-2222-2222-222222222222 was assigned to you.",
    relatedIncidentId: "22222222-2222-2222-2222-222222222222",
    createdAt: new Date().toISOString(),
    readAt: null,
    sourceEventId: "44444444-4444-4444-4444-444444444444",
    ...overrides,
  };
}

function jsonResponse(body: unknown, status = 200) {
  return Promise.resolve(
    new Response(JSON.stringify(body), {
      status,
      headers: { "content-type": "application/json" },
    }),
  );
}

test("login success redirects to incidents", async () => {
  vi.spyOn(globalThis, "fetch").mockImplementation((input) => {
    if (String(input).endsWith("/api/auth/login")) {
      return jsonResponse(loginResponse());
    }

    if (String(input).includes("/api/notifications/unread-count")) {
      return jsonResponse({ count: 0 });
    }

    return jsonResponse([]);
  });

  renderApp("/login");

  await userEvent.type(screen.getByLabelText(/email/i), "agent@airsms.test");
  await userEvent.type(screen.getByLabelText(/password/i), "correct-password");
  await userEvent.click(screen.getByRole("button", { name: /sign in/i }));

  await waitFor(() => {
    expect(screen.getByRole("heading", { name: "Incidents" })).toBeTruthy();
  });
});

test("login failure shows invalid credentials", async () => {
  vi.spyOn(globalThis, "fetch").mockImplementation(() =>
    jsonResponse({ title: "Authentication failed" }, 401),
  );

  renderApp("/login");

  await userEvent.type(screen.getByLabelText(/email/i), "agent@airsms.test");
  await userEvent.type(screen.getByLabelText(/password/i), "wrong-password");
  await userEvent.click(screen.getByRole("button", { name: /sign in/i }));

  expect(
    await screen.findByText("That email and password combination was not recognised."),
  ).toBeTruthy();
});

test("protected route redirects anonymous users to login", () => {
  renderApp("/incidents");

  expect(screen.getByRole("heading", { name: "Sign in" })).toBeTruthy();
});

test("incident list renders API results", async () => {
  signIn();
  vi.spyOn(globalThis, "fetch").mockImplementation((input) => {
    if (String(input).includes("/api/notifications/unread-count")) {
      return jsonResponse({ count: 0 });
    }

    return jsonResponse([incident()]);
  });

  renderApp("/incidents");

  expect(await screen.findByText("Hydraulic warning")).toBeTruthy();
  expect(screen.getAllByText("Technical").length).toBeGreaterThan(0);
  expect(screen.getAllByText("High").length).toBeGreaterThan(0);
});

test("workflow preview is role aware", async () => {
  signIn("Supervisor");
  vi.spyOn(globalThis, "fetch").mockImplementation((input) => {
    if (String(input).includes("/api/notifications/unread-count")) {
      return jsonResponse({ count: 0 });
    }

    return jsonResponse([]);
  });

  renderApp("/incidents");

  expect(await screen.findByText(/Workflow actions are available/)).toBeTruthy();
});

test("notifications page lists and marks a notification read", async () => {
  signIn();
  const unread = notification();

  vi.spyOn(globalThis, "fetch").mockImplementation((input) => {
    const url = String(input);

    if (url.includes("/api/notifications/unread-count")) {
      return jsonResponse({ count: 1 });
    }

    if (url.endsWith(`/api/notifications/${unread.id}/read`)) {
      return jsonResponse({ ...unread, readAt: new Date().toISOString() });
    }

    if (url.endsWith("/api/notifications")) {
      return jsonResponse([unread]);
    }

    return jsonResponse([]);
  });

  renderApp("/notifications");

  expect(await screen.findByRole("heading", { name: "Notifications" })).toBeTruthy();
  expect(await screen.findByText("Incident assigned")).toBeTruthy();

  await userEvent.click(screen.getByRole("button", { name: "Mark read" }));

  expect(await screen.findByRole("button", { name: "Read" })).toBeTruthy();
});
