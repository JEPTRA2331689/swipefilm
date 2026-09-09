import { api } from "@/lib/api";

// ✅ Étape explicite dans l'onboarding — jamais déduite automatiquement de
// l'URL du navigateur (localhost, mauvais port, etc. cassent l'enregistrement
// webhook Radarr/Sonarr). Modifiable ensuite dans Paramètres → Webhook.
export async function setupPublicUrl(
  url: string,
): Promise<{ publicUrl: string | null }> {
  return api.post("/api/setup/public-url", { url });
}

export async function skipPublicUrl(): Promise<void> {
  await api.post("/api/setup/public-url/skip");
}

// ── Adresse publique (modifiable après le setup, dans Paramètres) ──

export async function getPublicUrl(): Promise<string | null> {
  const res = await api.get<{ url: string | null }>("/api/settings/public-url");
  return res.url;
}

export async function setPublicUrl(url: string): Promise<string | null> {
  const res = await api.put<{ url: string | null }>(
    "/api/settings/public-url",
    { url },
  );
  return res.url;
}
