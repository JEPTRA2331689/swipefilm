import { api } from "./api";
import { setTokenCookie, clearTokenCookie } from "./jwt";
import { useAppStore } from "./store";
import type { User, UserServer } from "@/types";

export async function register(
  email: string,
  password: string,
  displayName: string,
): Promise<void> {
  const res = await api.post<{ token: string }>(
    "/api/auth/register",
    { email, password, displayName },
    { skipAuth: true },
  );
  if (res?.token) setTokenCookie(res.token);
}

export async function fetchMe(): Promise<User> {
  return api.get<User>("/api/auth/me");
}

// ── Mon profil ────────────────────────────────────────────────────

export interface UserProfile {
  id: string;
  email: string;
  displayName: string;
}

export interface UserPreferences {
  language?: string;
  emailNotifications?: boolean;
  [key: string]: unknown;
}

export interface UserQuota {
  used: number;
  limit: number;
  resetsAt?: string;
}

export async function getMyProfile(): Promise<UserProfile> {
  return api.get<UserProfile>("/api/users/me");
}

export async function updateMyProfile(
  data: Partial<Pick<UserProfile, "email" | "displayName">>,
): Promise<UserProfile> {
  return api.put<UserProfile>("/api/users/me", data);
}

export async function getMyPreferences(): Promise<UserPreferences> {
  return api.get<UserPreferences>("/api/users/me/preferences");
}

export async function updateMyPreferences(
  prefs: UserPreferences,
): Promise<UserPreferences> {
  return api.put<UserPreferences>("/api/users/me/preferences", prefs);
}

export async function getMyQuota(): Promise<UserQuota> {
  return api.get<UserQuota>("/api/users/me/quota");
}

export async function changePassword(
  currentPassword: string,
  newPassword: string,
): Promise<void> {
  await api.put("/api/users/me/password", { currentPassword, newPassword });
}

// ✅ ServerType (backend, Models/ServerConfig.cs) : Plex=0, Jellyfin=1
export function normalizeServer(raw: UserServer): UserServer {
  return {
    ...raw,
    type: raw.type === 0 ? "plex" : raw.type === 1 ? "jellyfin" : raw.type,
  };
}

export function logout() {
  clearTokenCookie();
  useAppStore.getState().logout();
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

// ── Permissions ───────────────────────────────────────────────────

export interface UserListItem {
  id: string;
  email: string;
  displayName: string;
  permissions: number;
  createdAt?: string;
}

export async function getUsers(): Promise<UserListItem[]> {
  return api.get<UserListItem[]>("/api/users");
}

export async function getUser(userId: string): Promise<UserListItem> {
  return api.get<UserListItem>(`/api/users/${userId}`);
}

export async function updateUser(
  userId: string,
  data: Partial<Pick<UserProfile, "email" | "displayName">>,
): Promise<UserListItem> {
  return api.put<UserListItem>(`/api/users/${userId}`, data);
}

export async function getUserPreferences(
  userId: string,
): Promise<UserPreferences> {
  return api.get<UserPreferences>(`/api/users/${userId}/preferences`);
}

export async function updateUserPreferencesById(
  userId: string,
  prefs: UserPreferences,
): Promise<UserPreferences> {
  return api.put<UserPreferences>(`/api/users/${userId}/preferences`, prefs);
}

export async function getUserQuota(userId: string): Promise<UserQuota> {
  return api.get<UserQuota>(`/api/users/${userId}/quota`);
}

export async function updateUserQuota(
  userId: string,
  quota: Partial<UserQuota>,
): Promise<UserQuota> {
  return api.put<UserQuota>(`/api/users/${userId}/quota`, quota);
}

export async function deleteUser(userId: string): Promise<void> {
  await api.delete(`/api/users/${userId}`);
}

export async function updateUserPermissions(
  userId: string,
  permissions: number,
): Promise<void> {
  await api.put(`/api/permissions/${userId}`, { permissions });
}

export async function applyPermissionPreset(
  userId: string,
  preset: "none" | "user" | "moderator" | "admin",
): Promise<void> {
  await api.put(`/api/permissions/${userId}/preset`, { preset });
}

// ── Requests ──────────────────────────────────────────────────────

// Valeurs réelles renvoyées par le backend (Models/Request.cs RequestStatus) :
// 0=Pending 1=Approved 2=Downloading 3=PartiallyAvailable 4=Available 5=Declined
// -1 est un sentinel client-only (toFakeRequest) — jamais renvoyé par l'API —
// pour représenter "aucune requête existante".
export type RequestStatusCode = -1 | 0 | 1 | 2 | 3 | 4 | 5;
export type RequestStatus =
  | "none"
  | "pending"
  | "approved"
  | "downloading"
  | "partially-available"
  | "available"
  | "declined";

export function statusFromCode(code: RequestStatusCode): RequestStatus {
  switch (code) {
    case -1:
      return "none";
    case 0:
      return "pending";
    case 1:
      return "approved";
    case 2:
      return "downloading";
    case 3:
      return "partially-available";
    case 4:
      return "available";
    case 5:
      return "declined";
  }
}

export interface RequestMovie {
  id: string;
  tmdbId: number;
  title: string;
  posterPath: string | null;
  tmdbRating: number;
  contentType: string;
}

export interface RequestSeries {
  id: string;
  tmdbId: number;
  title: string;
  posterPath: string | null;
  tmdbRating: number;
}

export interface RequestUser {
  id: string;
  displayName: string;
  avatarUrl?: string | null;
}

export interface MovieRequest {
  id: string;
  status: RequestStatusCode;
  type: number;
  requestedAt: string;
  processedAt: string | null;
  availableAt: string | null;
  declineReason: string | null;
  message: string | null;
  externalId: string | null;
  // ✅ Exactement un des deux est renseigné selon `type` (0=Movie, 1=Tv)
  movie: RequestMovie | null;
  series: RequestSeries | null;
  requestedBy: RequestUser;
  processedBy: RequestUser | null;
}

// Petit helper pour lire titre/poster/tmdbId/contentType sans se soucier
// de savoir si c'est movie ou series qui est renseigné.
export function requestMedia(r: MovieRequest): {
  id: string;
  tmdbId: number;
  title: string;
  posterPath: string | null;
  tmdbRating: number;
  contentType: string;
} {
  if (r.movie) return { ...r.movie };
  if (r.series) return { ...r.series, contentType: "series" };
  return {
    id: "",
    tmdbId: 0,
    title: "?",
    posterPath: null,
    tmdbRating: 0,
    contentType: "movie",
  };
}

interface PaginatedRequests {
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
  results: MovieRequest[];
}

export interface RequestCount {
  pending: number;
  approved: number;
  declined: number;
  total: number;
}

export async function getRequests(): Promise<MovieRequest[]> {
  const res = await api.get<MovieRequest[] | PaginatedRequests>(
    "/api/requests",
  );
  if (Array.isArray(res)) return res;
  if ("results" in res) return res.results;
  return [];
}

export async function getRequestCount(): Promise<RequestCount> {
  return api.get<RequestCount>("/api/requests/count");
}

export interface RequestWrapper {
  hasRequest: boolean;
  isMyRequest: boolean;
  request: MovieRequest | null;
}

export async function getRequest(id: string): Promise<RequestWrapper> {
  return api.get<RequestWrapper>(`/api/requests/${id}`);
}

export async function getRequestByMovieWrapped(
  tmdbId: number,
): Promise<RequestWrapper> {
  try {
    return await api.get<RequestWrapper>(`/api/requests/movie/${tmdbId}`);
  } catch {
    return { hasRequest: false, isMyRequest: false, request: null };
  }
}

export async function getRequestBySeriesWrapped(
  tmdbId: number,
): Promise<RequestWrapper> {
  try {
    return await api.get<RequestWrapper>(`/api/requests/series/${tmdbId}`);
  } catch {
    return { hasRequest: false, isMyRequest: false, request: null };
  }
}

export async function getRequestBySeries(
  tmdbId: number,
): Promise<MovieRequest | null> {
  const res = await getRequestBySeriesWrapped(tmdbId);
  return res.hasRequest ? res.request : null;
}

// ✅ Le backend attend movieId OU seriesId (jamais les deux) — pas de champ
// "mediaType", le type est déduit de celui des deux IDs qui est renseigné.
// seasonNumbers ne s'applique qu'aux séries — absent/vide = série entière.
export async function createRequest(params: {
  movieId?: string;
  seriesId?: string;
  message?: string;
  seasonNumbers?: number[];
  qualityProfileId?: number;
  rootFolderPath?: string;
}): Promise<MovieRequest> {
  return api.post<MovieRequest>("/api/requests", params);
}

export async function approveRequest(id: string): Promise<void> {
  await api.put(`/api/requests/${id}/approve`);
}

export async function declineRequest(
  id: string,
  reason: string,
): Promise<void> {
  await api.put(`/api/requests/${id}/decline`, { reason });
}

export async function cancelRequest(id: string): Promise<void> {
  await api.delete(`/api/requests/${id}`);
}

export async function getRequestByMovie(
  tmdbId: number,
): Promise<MovieRequest | null> {
  try {
    const res = await api.get<RequestWrapper>(`/api/requests/movie/${tmdbId}`);
    return res.hasRequest ? res.request : null;
  } catch {
    return null;
  }
}

export async function getDefaultPermissions(): Promise<{
  defaultPermissions: number;
}> {
  return api.get("/api/permissions/defaults");
}

export async function updateDefaultPermissions(
  permissions: number,
): Promise<void> {
  await api.put("/api/permissions/defaults", { permissions });
}

// ── Tâches planifiées (Hangfire) — réservé Permission.Admin côté backend ──

export interface JobDefinition {
  id: string;
  label: string;
  description: string;
  lastExecution: string | null;
  nextExecution: string | null;
}

export async function getJobs(): Promise<JobDefinition[]> {
  return api.get<JobDefinition[]>("/api/jobs");
}

export async function runJob(
  id: string,
): Promise<{ message: string; jobId: string; trackUrl: string }> {
  return api.post(`/api/jobs/${id}/run`);
}

// ── Logs (Serilog) — réservé Permission.Admin côté backend ─────────

export interface LogEntry {
  timestamp: string;
  level: string;
  sourceContext: string | null;
  message: string;
}

export interface LogsResponse {
  total: number;
  page: number;
  pageSize: number;
  results: LogEntry[];
}

export async function getLogs(params: {
  page?: number;
  pageSize?: number;
  level?: string;
  search?: string;
}): Promise<LogsResponse> {
  const query = new URLSearchParams();
  if (params.page) query.set("page", String(params.page));
  if (params.pageSize) query.set("pageSize", String(params.pageSize));
  if (params.level) query.set("level", params.level);
  if (params.search) query.set("search", params.search);
  return api.get<LogsResponse>(`/api/logs?${query.toString()}`);
}

// ── Overseerr / Seerr ─────────────────────────────────────────────

export async function configureSeerr(
  url: string,
  apiKey: string,
): Promise<void> {
  await api.post("/api/seerr/configure", { url, apiKey });
}

// ── Setup (première installation) ─────────────────────────────────

export async function checkSetupStatus(): Promise<{ isComplete: boolean }> {
  return api.get<{ isComplete: boolean }>("/api/setup/status", {
    skipAuth: true,
  });
}

export async function setupAdmin(
  email: string,
  password: string,
  displayName: string,
): Promise<{ token?: string }> {
  const res = await api.post<{ token?: string }>(
    "/api/setup/admin",
    {
      email,
      password,
      displayName,
    },
    { skipAuth: true },
  );
  if (res?.token) setTokenCookie(res.token);
  return res;
}

// ✅ Le backend accepte soit apiKey, soit username/password (Jellyfin/Plex
// gèrent les deux) — jamais les deux en même temps que vide.
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

export async function setupSeerr(url: string, apiKey: string): Promise<void> {
  await api.post("/api/setup/seerr", { url, apiKey });
}

export async function skipSeerr(): Promise<void> {
  await api.post("/api/setup/seerr/skip");
}

// ── Radarr ────────────────────────────────────────────────────────

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

export async function getRadarr(): Promise<ArrConfig | null> {
  try {
    return await api.get<ArrConfig>("/api/radarr/config");
  } catch {
    return null;
  }
}

export async function updateRadarr(
  url: string,
  apiKey: string,
  prefs?: Partial<ArrPreferences>,
): Promise<{ message: string; testConnectionOk: boolean }> {
  return api.put("/api/radarr/config", { url, apiKey, ...prefs });
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

export async function skipRadarr(): Promise<void> {
  await api.post("/api/setup/radarr/skip");
}

// ── Sonarr ────────────────────────────────────────────────────────

export async function getSonarr(): Promise<ArrConfig | null> {
  try {
    return await api.get<ArrConfig>("/api/sonarr/config");
  } catch {
    return null;
  }
}

export async function updateSonarr(
  url: string,
  apiKey: string,
  prefs?: Partial<ArrPreferences>,
): Promise<{ message: string; testConnectionOk: boolean }> {
  return api.put("/api/sonarr/config", { url, apiKey, ...prefs });
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

export async function skipSonarr(): Promise<void> {
  await api.post("/api/setup/sonarr/skip");
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
