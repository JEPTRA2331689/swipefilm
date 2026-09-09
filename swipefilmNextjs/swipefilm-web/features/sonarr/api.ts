import { api } from "@/lib/api";
import type {
  ArrConfig,
  ArrStatus,
  ArrProfile,
  ArrRootFolder,
  ArrPreferences,
  WebhookVerifyResult,
} from "@/shared/types/arr";

export async function getSonarr(): Promise<ArrConfig | null> {
  try {
    return await api.get<ArrConfig>("/api/sonarr");
  } catch {
    return null;
  }
}

export async function updateSonarr(
  url: string,
  apiKey: string,
  prefs?: Partial<ArrPreferences>,
): Promise<{ message: string; testConnectionOk: boolean }> {
  return api.put("/api/sonarr", { url, apiKey, ...prefs });
}

export async function getSonarrStatus(): Promise<ArrStatus> {
  return api.get<ArrStatus>("/api/sonarr/status");
}

export async function testSonarr(url: string, apiKey: string): Promise<void> {
  await api.post("/api/sonarr/test", { url, apiKey });
}

export async function getSonarrQualityProfiles(): Promise<ArrProfile[]> {
  return api.get<ArrProfile[]>("/api/sonarr/quality-profiles");
}

export async function getSonarrRootFolders(): Promise<ArrRootFolder[]> {
  return api.get<ArrRootFolder[]>("/api/sonarr/root-folders");
}

export async function setupSonarr(url: string, apiKey: string): Promise<void> {
  await api.post("/api/setup/sonarr", { url, apiKey });
}

// ✅ (Ré)enregistre le webhook Sonarr → SwipeFilm à la demande — utilisé par
// le bouton "Vérifier le webhook" dans l'onboarding et les paramètres.
export async function verifySonarrWebhook(): Promise<WebhookVerifyResult> {
  return api.post("/api/sonarr/webhook/verify");
}

export async function skipSonarr(): Promise<void> {
  await api.post("/api/setup/sonarr/skip");
}
