import { createContext, useCallback, useContext, useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import type { LoginResponse, Member } from "../api/types";
import { setAuthToken, setUnauthorizedHandler } from "../api/client";
import { login as loginRequest } from "../api/folio";

const STORAGE_KEY = "folio.auth";

interface StoredSession {
  token: string;
  member: Member;
}

interface AuthContextValue {
  member: Member | null;
  token: string | null;
  isAuthenticated: boolean;
  login: (email: string, password: string) => Promise<LoginResponse>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

function readSession(): StoredSession | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as StoredSession) : null;
  } catch {
    return null;
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<StoredSession | null>(() => {
    const stored = readSession();
    // Prime the API client before the first request goes out.
    setAuthToken(stored?.token ?? null);
    return stored;
  });

  const logout = useCallback(() => {
    localStorage.removeItem(STORAGE_KEY);
    setAuthToken(null);
    setSession(null);
  }, []);

  // A 401 from any request means the token is gone/stale — drop the session.
  useEffect(() => {
    setUnauthorizedHandler(logout);
    return () => setUnauthorizedHandler(null);
  }, [logout]);

  const login = useCallback(async (email: string, password: string) => {
    const result = await loginRequest({ email, password });
    const next: StoredSession = { token: result.token, member: result.member };
    localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
    setAuthToken(result.token);
    setSession(next);
    return result;
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      member: session?.member ?? null,
      token: session?.token ?? null,
      isAuthenticated: session !== null,
      login,
      logout,
    }),
    [session, login, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return ctx;
}
