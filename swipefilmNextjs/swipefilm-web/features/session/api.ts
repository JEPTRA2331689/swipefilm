import { api } from "@/lib/api";
import type { ContentTypeFilter, Movie, SwipeDirection } from "@/types";

export type SessionStatus = 0 | 1 | 2; // Active | Completed | Expired

export interface SessionMember {
  userId: string;
  displayName: string;
  avatarUrl: string | null;
  isGuest: boolean;
  isHost: boolean;
  swipeCount: number;
}

export interface SessionState {
  id: string;
  code: string;
  status: SessionStatus;
  hostUserId: string;
  genreFilter: string[] | null;
  contentTypeFilter: ContentTypeFilter;
  hasLaunched: boolean;
  hasStarted: boolean;
  members: SessionMember[];
}

export interface SessionMatchResult {
  movieId: string | null;
  seriesId: string | null;
  title: string;
  posterPath: string | null;
  agreeCount: number;
  memberCount: number;
  agreePct: number;
  isAvailable: boolean;
  jellyfinUrl: string | null;
  plexUrl: string | null;
}

export async function createSession(): Promise<SessionState> {
  return api.post<SessionState>("/api/sessions");
}

export async function getSessionState(code: string): Promise<SessionState> {
  return api.get<SessionState>(`/api/sessions/${code}`);
}

// ✅ Connecté (cookie déjà présent) → displayName ignoré côté backend. Sans
// cookie → displayName requis, un compte invité éphémère est provisionné et
// connecté automatiquement (même cookie de session que login/register).
export async function joinSession(
  code: string,
  displayName?: string,
): Promise<SessionState> {
  return api.post<SessionState>("/api/sessions/join", { code, displayName });
}

// ✅ Quitte l'écran de lobby (QR/code) pour l'écran de configuration —
// le QR/code n'est plus montré une fois cette étape passée.
export async function launchSession(code: string): Promise<SessionState> {
  return api.post<SessionState>(`/api/sessions/${code}/launch`);
}

export async function setSessionGenre(
  code: string,
  genres: string[] | null,
  contentType: ContentTypeFilter,
): Promise<SessionState> {
  return api.post<SessionState>(`/api/sessions/${code}/genre`, {
    genres,
    contentType,
  });
}

// ✅ Étape finale de l'onboarding hôte, après le genre — durationMinutes
// absent/null garde la durée par défaut (6h) fixée à la création.
export async function startSession(
  code: string,
  durationMinutes: number | null,
): Promise<SessionState> {
  return api.post<SessionState>(`/api/sessions/${code}/start`, {
    durationMinutes,
  });
}

export async function getSessionPool(
  code: string,
  count = 40,
): Promise<{ pool: Movie[]; refined: boolean }> {
  return api.get(`/api/sessions/${code}/pool?count=${count}`);
}

export async function sendSessionSwipe(
  code: string,
  params: {
    movieId?: string;
    seriesId?: string;
    direction: SwipeDirection;
    durationMs: number;
  },
): Promise<void> {
  await api.post(`/api/sessions/${code}/swipe`, {
    movieId: params.movieId,
    seriesId: params.seriesId,
    direction: params.direction === "Right" ? 1 : 0,
    durationMs: params.durationMs,
  });
}

export async function getSessionMatches(
  code: string,
): Promise<SessionMatchResult[]> {
  return api.get(`/api/sessions/${code}/matches`);
}

export async function revealSessionMatches(
  code: string,
): Promise<SessionMatchResult[]> {
  return api.post(`/api/sessions/${code}/reveal`);
}
