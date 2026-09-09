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
  logout: () => Promise<void>;
}

const defaultCtx: AuthContextType = {
  user: null,
  loading: true,
  hasPermission: () => false,
  isAdmin: false,
  login: async (_username: string, _password: string) => {},
  logout: async () => {},
};

const AuthContext = createContext<AuthContextType>(defaultCtx);

// ✅ Forme renvoyée par /api/auth/login, /register et /me — même DTO
// (AuthController.BuildMeDto) sur les trois, pas de round-trip supplémentaire
// après login/register.
interface MeResponse {
  id: string;
  email: string;
  displayName: string;
  permissions: number;
}

function toAuthUser(raw: MeResponse): AuthUser {
  return {
    id: raw.id,
    email: raw.email,
    displayName: raw.displayName,
    permissions: raw.permissions,
  };
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const [user, setUser] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    // ✅ Le cookie de session est HttpOnly — invisible en JS, impossible à
    // décoder côté client comme avec le JWT. La seule façon de savoir si on
    // est connecté est de demander au serveur.
    api
      .get<MeResponse>("/api/auth/me")
      .then((raw) => setUser(toAuthUser(raw)))
      .catch(() => setUser(null))
      .finally(() => setLoading(false));
  }, []);

  const login = useCallback(async (username: string, password: string) => {
    try {
      const res = await api.post<MeResponse>(
        "/api/auth/login",
        { username, password },
        { skipAuth: true },
      );
      setUser(toAuthUser(res));
    } catch {
      throw new Error("Nom d'utilisateur ou mot de passe invalide.");
    }
  }, []);

  const logout = useCallback(async () => {
    // ✅ Révoque la session en base (supprime la ligne AuthSessions) — un
    // clear côté client ne suffirait pas, contrairement au JWT qui restait
    // valide jusqu'à expiration de toute façon.
    await api.post("/api/auth/logout").catch(() => {});
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
