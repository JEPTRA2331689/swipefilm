"use client";

import React, {
  createContext,
  useContext,
  useEffect,
  useState,
  useCallback,
  useMemo,
} from "react";
import { useRouter } from "next/navigation";
import { Permission, hasPermission as checkPerm } from "@/lib/permissions";
import {
  decodeJwt,
  getTokenFromCookie,
  setTokenCookie,
  clearTokenCookie,
  isTokenExpired,
} from "@/lib/jwt";
import { api } from "@/lib/api";

interface AuthUser {
  id: string;
  email: string;
  displayName: string;
  permissions: number;
}

interface AuthContextType {
  user: AuthUser | null;
  loading: boolean;
  hasPermission: (permission: Permission) => boolean;
  isAdmin: boolean;
  login: (username: string, password: string) => Promise<void>;
  logout: () => void;
}

const defaultCtx: AuthContextType = {
  user: null,
  loading: true,
  hasPermission: () => false,
  isAdmin: false,
  login: async (_username: string, _password: string) => {},
  logout: () => {},
};

const AuthContext = createContext<AuthContextType>(defaultCtx);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const [user, setUser] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const token = getTokenFromCookie();
    if (!token) {
      setLoading(false);
      return;
    }

    const payload = decodeJwt(token);
    if (!payload || isTokenExpired(payload)) {
      clearTokenCookie();
      setLoading(false);
      return;
    }

    setUser({
      id: payload.nameid,
      email: payload.email,
      displayName: payload.unique_name,
      permissions: parseInt(payload.permissions ?? "0", 10),
    });
    setLoading(false);
  }, []);

  const login = useCallback(async (username: string, password: string) => {
    try {
      const res = await api.post<{ token: string }>(
        "/api/Auth/login",
        { username: username, password },
        { skipAuth: true },
      );
      setTokenCookie(res.token);
      console.log(document.cookie);
      console.log(getTokenFromCookie());
      const payload = decodeJwt(res.token);
      console.log(payload);
      console.log(JSON.parse(atob(res.token.split(".")[1])));
      if (!payload) throw new Error("Token invalide");
      setUser({
        id: payload.nameid,
        email: payload.email,
        displayName: payload.unique_name,
        permissions: parseInt(payload.permissions ?? "0", 10),
      });
    } catch {
      throw new Error("Nom d'utilisateur ou mot de passe invalide.");
    }
  }, []);

  const logout = useCallback(() => {
    clearTokenCookie();
    setUser(null);
    router.replace("/login");
  }, [router]);

  const hasPermission = useCallback(
    (permission: Permission) => {
      if (!user) return false;
      return checkPerm(user.permissions, permission);
    },
    [user],
  );

  const isAdmin = hasPermission(Permission.Admin);

  const value = useMemo(
    () => ({ user, loading, hasPermission, isAdmin, login, logout }),
    [user, loading, hasPermission, isAdmin, login, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextType {
  return useContext(AuthContext);
}
