// ✅ Types partagés entre features/radarr et features/sonarr — même forme
// exacte côté backend (RadarrController/SonarrController renvoient la même
// enveloppe JSON pour les deux services).

export interface ArrConfig {
  url: string;
  apiKeyHint: string; // 4 premiers caractères de la clé
  isConfigured: boolean;
  defaultQualityProfileId?: number;
  defaultRootFolderPath?: string;
}

export interface ArrStatus {
  isConnected: boolean;
  version: string;
}

export interface ArrProfile {
  id: number;
  name: string;
}

export interface ArrRootFolder {
  path: string;
  freeSpaceGb?: number;
}

export interface ArrPreferences {
  defaultQualityProfileId?: number;
  defaultRootFolderPath?: string;
}

export interface WebhookVerifyResult {
  success: boolean;
  callbackUrl: string;
  error?: string | null;
}
