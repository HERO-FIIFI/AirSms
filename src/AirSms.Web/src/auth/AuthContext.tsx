import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type PropsWithChildren,
} from "react";
import { loginRequest } from "../api/authApi";
import { onUnauthorized, setAccessToken } from "../api/client";
import type { LoginResponse, User } from "../types/auth";

type Session = LoginResponse;

type AuthContextValue = {
  user: User | null;
  accessToken: string | null;
  isAuthenticated: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
};

const sessionKey = "airsms.session";
const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: PropsWithChildren) {
  const [session, setSession] = useState<Session | null>(() => readSession());

  useEffect(() => {
    setAccessToken(session?.accessToken ?? null);
  }, [session]);

  const logout = useCallback(() => {
    sessionStorage.removeItem(sessionKey);
    setSession(null);
    setAccessToken(null);
  }, []);

  useEffect(() => onUnauthorized(logout), [logout]);

  const login = useCallback(async (email: string, password: string) => {
    const nextSession = await loginRequest(email, password);
    sessionStorage.setItem(sessionKey, JSON.stringify(nextSession));
    setSession(nextSession);
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      user: session?.user ?? null,
      accessToken: session?.accessToken ?? null,
      isAuthenticated: Boolean(session?.accessToken),
      login,
      logout,
    }),
    [login, logout, session],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const value = useContext(AuthContext);
  if (!value) {
    throw new Error("useAuth must be used inside AuthProvider.");
  }

  return value;
}

function readSession(): Session | null {
  const raw = sessionStorage.getItem(sessionKey);
  if (!raw) {
    return null;
  }

  try {
    return JSON.parse(raw) as Session;
  } catch {
    sessionStorage.removeItem(sessionKey);
    return null;
  }
}
