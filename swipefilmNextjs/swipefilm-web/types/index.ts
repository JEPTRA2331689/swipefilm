// ✅ Correspond aux valeurs réellement renvoyées par le backend
// (Movie.ContentType / HomeSectionMovie.ContentType) — "series", pas "tv".
export type ContentType = "movie" | "series";

// 0 = All, 1 = AvailableOnly, 2 = UnavailableOnly
export type AvailabilityFilter = 0 | 1 | 2;

// 0 = All, 1 = Movie, 2 = TV
export type ContentTypeFilter = 0 | 1 | 2;

export interface Movie {
  id: string;
  tmdbId: number;
  title: string;
  posterPath: string | null;
  backdropPath: string | null;
  overview: string | null;
  tmdbRating: number;
  runtimeMinutes: number | null;
  genres: string[];
  releaseDate: string | null;
  contentType: ContentType;
  isAvailable: boolean;
  // Liens directs vers la fiche du film — un champ par type de serveur, un
  // utilisateur pouvant avoir Jellyfin ET Plex (voir MovieController.GetMovie)
  jellyfinUrl?: string | null;
  plexUrl?: string | null;
  score?: number;
  castTop5?: string[];
  directors?: string[];
}

// GET /api/series/{id} — champs propres aux séries (pas de runtime unique,
// saisons/épisodes/créateurs à la place de durée/réalisateurs)
export interface Series {
  id: string;
  tmdbId: number;
  title: string;
  originalTitle: string | null;
  posterPath: string | null;
  backdropPath: string | null;
  overview: string | null;
  certification: string | null;
  tmdbRating: number;
  tmdbPopularity: number;
  numberOfSeasons: number;
  numberOfEpisodes: number;
  genres: string[];
  createdBy: string[];
  castTop10: string[];
  firstAirDate: string | null;
  contentType: "series";
  isAvailable: boolean;
  jellyfinUrl?: string | null;
  plexUrl?: string | null;
  requestId: string | null;
  requestStatus: number | null;
}

// GET /api/series/{id}/seasons
// requestStatus — valeurs réelles du backend (Models/Request.cs RequestStatus) :
// 0=Pending 1=Approved 2=Downloading 3=PartiallyAvailable 4=Available 5=Declined
export interface SeriesSeason {
  seasonNumber: number;
  episodeCount: number;
  posterPath: string | null;
  isAvailable: boolean;
  requestId: string | null;
  requestStatus: number | null;
}

export interface HomeSection {
  id: string;
  title: string;
  movies: Movie[];
}

export interface UserProfile {
  genreWeights: Record<string, number>;
  actorWeights: Record<string, number>;
  directorWeights: Record<string, number>;
  keywordWeights: Record<string, number>;
  originalLanguageWeights: Record<string, number>;
  preferredDecadeWeights: Record<string, number>;
  totalSignals: number;
  preferredMinYear: number;
  preferredRuntimeMax: number;
}

export type SwipeDirection = "Left" | "Right";

export interface SwipePayload {
  movieId: string;
  direction: 0 | 1;
  durationMs: number;
  context?: number;
}

export interface User {
  id: string;
  email: string;
  name: string;
  permissions: number;
  // ✅ Un seul serveur pour toute l'instance — plus une liste par
  // utilisateur (voir AuthController.Me)
  server?: UserServer | null;
}

export interface AuthResponse {
  token: string;
  user: User;
}

export interface ArrConfig {
  url: string;
  apiKey: string;
}

// ✅ Singleton — un seul serveur pour toute l'instance (voir GET /api/servers).
// Vit dans config/settings.json côté backend (pas en base) — pas d'id/createdAt.
export interface UserServer {
  friendlyName?: string;
  type: "plex" | "jellyfin" | 0 | 1;
  lastSyncAt?: string;
}
