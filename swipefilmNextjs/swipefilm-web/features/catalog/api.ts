import { api } from "@/lib/api";
import type { Movie, AvailabilityFilter, ContentTypeFilter } from "@/types";

// ✅ Feature "catalog" — films et séries partagent le même flux de requête
// (un seul MovieRequest, avec soit `movie` soit `series` renseigné) donc pas
// de séparation features/movies vs features/series : un seul domaine.

// ── Exploration ("voir plus" + navigation par genre) ───────────────

interface BrowseResponse {
  title: string;
  movies: Movie[];
  hasMore: boolean;
}

// ✅ Même mécanisme "Parce que vous avez aimé X" que la home, exposé pour un
// film OU une série pivot — un seul composant SimilarMedia pour les deux.
export async function getSimilar(params: {
  id: string;
  isSeries: boolean;
  title?: string;
  count?: number;
}): Promise<Movie[]> {
  const q = new URLSearchParams({
    id: params.id,
    isSeries: String(params.isSeries),
  });
  if (params.title) q.set("title", params.title);
  if (params.count) q.set("count", String(params.count));
  return api.get<Movie[]>(`/api/recommendations/similar?${q}`);
}

// ✅ Reprend une section précise (algorithmique ou "genre_X", film ou série)
// avec un lot supplémentaire — excludeIds s'accumule côté appelant au fil du
// scroll, même principe que useHomeSections.
export async function browseSection(params: {
  sectionId: string;
  pageSize?: number;
  availability?: AvailabilityFilter;
  excludeIds?: number[];
}): Promise<BrowseResponse> {
  const q = new URLSearchParams({ sectionId: params.sectionId });
  if (params.pageSize) q.set("pageSize", String(params.pageSize));
  if (params.availability) q.set("availability", String(params.availability));
  if (params.excludeIds?.length) q.set("excludeIds", params.excludeIds.join(","));
  return api.get<BrowseResponse>(`/api/recommendations/browse?${q}`);
}

// ✅ Mélange films et séries d'un genre plutôt que deux listes séparées.
export async function browseGenre(params: {
  genre: string;
  pageSize?: number;
  availability?: AvailabilityFilter;
  contentType?: ContentTypeFilter;
  excludeIds?: number[];
}): Promise<BrowseResponse> {
  const q = new URLSearchParams({ genre: params.genre });
  if (params.pageSize) q.set("pageSize", String(params.pageSize));
  if (params.availability) q.set("availability", String(params.availability));
  if (params.contentType) q.set("contentType", String(params.contentType));
  if (params.excludeIds?.length) q.set("excludeIds", params.excludeIds.join(","));
  return api.get<BrowseResponse>(`/api/recommendations/browse-genre?${q}`);
}

export interface GenreCard {
  genre: string;
  backdropPath: string | null;
}

export async function getGenreCards(): Promise<GenreCard[]> {
  return api.get<GenreCard[]>("/api/recommendations/genres");
}

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
