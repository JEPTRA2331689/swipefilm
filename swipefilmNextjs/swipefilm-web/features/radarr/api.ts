import { api } from "@/lib/api";
import type {
  ArrConfig,
  ArrStatus,
  ArrProfile,
  ArrRootFolder,
  ArrPreferences,
  WebhookVerifyResult,
} from "@/shared/types/arr";

export async function getRadarr(): Promise<ArrConfig | null> {
  try {
    return await api.get<ArrConfig>("/api/radarr");
  } catch {
    return null;
  }
}

export async function updateRadarr(
  url: string,
  apiKey: string,
  prefs?: Partial<ArrPreferences>,
): Promise<{ message: string; testConnectionOk: boolean }> {
  return api.put("/api/radarr", { url, apiKey, ...prefs });
}

export async function getRadarrStatus(): Promise<ArrStatus> {
  return api.get<ArrStatus>("/api/radarr/status");
}

// ✅ Test sans sauvegarder — utilisé par l'étape "Tester la connexion" de l'onboarding
export async function testRadarr(url: string, apiKey: string): Promise<void> {
  await api.post("/api/radarr/test", { url, apiKey });
}

export async function getRadarrQualityProfiles(): Promise<ArrProfile[]> {
  return api.get<ArrProfile[]>("/api/radarr/quality-profiles");
}

export async function getRadarrRootFolders(): Promise<ArrRootFolder[]> {
  return api.get<ArrRootFolder[]>("/api/radarr/root-folders");
}

export async function addRadarrMovie(
  tmdbId: number,
  overrides?: { qualityProfileId?: number; rootFolderPath?: string },
): Promise<{ message: string; radarrMovieId: number }> {
  return api.post(`/api/radarr/movie/${tmdbId}`, overrides ?? {});
}

export async function removeRadarrMovie(
  tmdbId: number,
  deleteFiles = false,
): Promise<void> {
  await api.delete(`/api/radarr/movie/${tmdbId}?deleteFiles=${deleteFiles}`);
}

export async function setupRadarr(url: string, apiKey: string): Promise<void> {
  await api.post("/api/setup/radarr", { url, apiKey });
}

// ✅ (Ré)enregistre le webhook Radarr → SwipeFilm à la demande — utilisé par
// le bouton "Vérifier le webhook" dans l'onboarding et les paramètres.
export async function verifyRadarrWebhook(): Promise<WebhookVerifyResult> {
  return api.post("/api/radarr/webhook/verify");
}

export async function skipRadarr(): Promise<void> {
  await api.post("/api/setup/radarr/skip");
}
