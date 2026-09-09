import { api } from "@/lib/api";
import type { UserServer } from "@/types";

// ✅ ServerType (backend, Models/ServerConfig.cs) : Plex=0, Jellyfin=1
export function normalizeServer(raw: UserServer): UserServer {
  return {
    ...raw,
    type: raw.type === 0 ? "plex" : raw.type === 1 ? "jellyfin" : raw.type,
  };
}

// ── Server (singleton — un seul serveur pour toute l'instance) ─────

export interface ConfigureServerPayload {
  friendlyName: string;
  type: "plex" | "jellyfin";
  // ✅ Soit apiKey (Jellyfin) / token direct (Plex), soit username+password
  apiKey?: string;
  username?: string;
  password?: string;
  url?: string;
}

// ✅ Admin uniquement — configure (crée ou met à jour) l'unique serveur
export async function configureServer(
  payload: ConfigureServerPayload,
): Promise<UserServer> {
  return api.post<UserServer>("/api/servers", {
    friendlyName: payload.friendlyName,
    type: payload.type === "jellyfin" ? 1 : 0, // ServerType enum: Plex=0, Jellyfin=1
    apiKey: payload.apiKey,
    username: payload.username,
    password: payload.password,
    url: payload.url,
  });
}

export async function getServerConfig(): Promise<UserServer | null> {
  return api.get<UserServer | null>("/api/servers");
}

export async function syncServer(): Promise<void> {
  await api.post("/api/servers/sync");
}

// ✅ Contrairement à syncServer() (/api/servers/sync), renvoie le jobId —
// nécessaire pour suivre la fin de la sync (voir waitForJob) plutôt que de
// juste la déclencher à l'aveugle. Utilisé par l'écran de synchronisation
// post-onboarding.
export async function triggerSync(): Promise<{ jobId: string }> {
  return api.post("/api/sync");
}

// ✅ Synchrone côté backend (pas de job Hangfire) — la réponse HTTP
// n'arrive qu'une fois la découverte terminée, rien à poller.
export async function discoverRecommendations(): Promise<void> {
  await api.post("/api/recommendations/discover");
}

export async function setupServer(payload: {
  friendlyName: string;
  type: 0 | 1;
  url: string;
  apiKey?: string;
  username?: string;
  password?: string;
}): Promise<void> {
  await api.post("/api/setup/server", payload);
}
