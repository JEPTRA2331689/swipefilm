import { api, setToken, clearToken } from "./api";
import { useAppStore } from "./store";
import type { User, UserServer } from "@/types";

// ── Auth ──────────────────────────────────────────────────────────

export async function register(
  email: string,
  password: string,
  displayName: string
): Promise<string> {
  const res = await api.post<{ token: string }>("/api/auth/register", {
    email,
    password,
    displayName,
  });
  setToken(res.token);
  return res.token;
}

export async function login(email: string, password: string): Promise<string> {
  const res = await api.post<{ token: string }>("/api/auth/login", {
    email,
    password,
  });
  setToken(res.token);
  return res.token;
}

export async function fetchMe(): Promise<User> {
  return api.get<User>("/api/auth/me");
}

export function logout() {
  clearToken();
  useAppStore.getState().logout();
}

// ── Servers ───────────────────────────────────────────────────────

export interface AddServerPayload {
  friendlyName: string;
  type: "plex" | "jellyfin";
  username: string;
  password: string;
  url?: string;
}

export async function addServer(payload: AddServerPayload): Promise<UserServer> {
  return api.post<UserServer>("/api/servers", {
    friendlyName: payload.friendlyName,
    type: payload.type === "jellyfin" ? 0 : 1, // ServerType enum: Jellyfin=0, Plex=1
    username: payload.username,
    password: payload.password,
    url: payload.url,
  });
}

export async function getServers(): Promise<UserServer[]> {
  return api.get<UserServer[]>("/api/servers");
}

export async function deleteServer(serverId: string): Promise<void> {
  return api.delete(`/api/servers/${serverId}`);
}

// ── Overseerr / Seerr ─────────────────────────────────────────────

export async function configureSeerr(url: string, apiKey: string): Promise<void> {
  await api.post("/api/seerr/configure", { url, apiKey });
}