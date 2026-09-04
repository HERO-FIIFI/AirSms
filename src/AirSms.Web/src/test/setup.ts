import { afterEach, vi } from "vitest";

afterEach(() => {
  sessionStorage.clear();
  vi.restoreAllMocks();
});
