import { api } from "@/lib/api";
import type { User, UserServer } from "@/types";

// ✅ Correspond à GET /api/auth/me (AuthController.Me) — le backend renvoie
// displayName/server, pas name — le mapping se fait ici, une seule fois,
// plutôt que d'un cast bricolé à chaque appelant.
interface MeResponse {
  id: string;
  email: string;
  displayName: string;
  permissions: number;
  server?: UserServer | null;
}

function mapMeResponse(raw: MeResponse): User {
  return {
    id: raw.id,
    email: raw.email,
    name: raw.displayName,
    permissions: raw.permissions,
    server: raw.server ?? null,
  };
}

export async function fetchMe(): Promise<User> {
  const raw = await api.get<MeResponse>("/api/auth/me");
  return mapMeResponse(raw);
}

// ── Overseerr / Seerr ─────────────────────────────────────────────

export async function configureSeerr(
  url: string,
  apiKey: string,
): Promise<void> {
  await api.post("/api/seerr/configure", { url, apiKey });
}

// ── Setup (première installation) ─────────────────────────────────

export async function checkSetupStatus(): Promise<{ isSetupComplete: boolean }> {
  return api.get<{ isSetupComplete: boolean }>("/api/setup/status", {
    skipAuth: true,
  });
}

// ✅ Le backend connecte directement (cookie de session) — plus de token à
// gérer côté client.
export async function setupAdmin(
  email: string,
  password: string,
  displayName: string,
): Promise<{ userId: string }> {
  return api.post<{ userId: string }>(
    "/api/setup/admin",
    {
      email,
      password,
      displayName,
    },
    { skipAuth: true },
  );
}

export async function setupSeerr(url: string, apiKey: string): Promise<void> {
  await api.post("/api/setup/seerr", { url, apiKey });
}

export async function skipSeerr(): Promise<void> {
  await api.post("/api/setup/seerr/skip");
}
