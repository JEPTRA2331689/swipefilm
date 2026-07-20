export type ContentType = "movie" | "tv";

export type AvailabilityFilter = "All" | "AvailableOnly" | "UnavailableOnly";

export interface Movie {
  id: string;
  tmdbId: number;
  title: string;
  posterPath: string | null;
  overview: string | null;
  tmdbRating: number;
  runtimeMinutes: number | null;
  genres: string[];
  releaseDate: string | null;
  contentType: ContentType;
  isAvailable: boolean;
  score?: number;
  castTop5?: string[];
  directors?: string[];
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
}

export interface AuthResponse {
  token: string;
  user: User;
}

export interface UserServer {
  id: string;
  name: string;
  type: "plex" | "jellyfin";
}
