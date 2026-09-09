"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { useAppStore } from "@/lib/store";
import type { Movie } from "@/types";

interface TmdbExtra {
  directors: string[];
  cast: { name: string; character: string; profilePath: string | null }[];
  backdropPath: string | null;
  tagline: string | null;
}

interface MovieDetailData {
  movie: Movie | null;
  extra: TmdbExtra | null;
  loading: boolean;
  error: string | null;
}

// ✅ "Films similaires" est géré par SimilarMedia (self-contained, features/
// catalog) — plus de fetch dupliqué ici.
export function useMovieDetail(movieId: string | null): MovieDetailData {
  const user = useAppStore((s) => s.user);
  const [movie, setMovie] = useState<Movie | null>(null);
  const [extra, setExtra] = useState<TmdbExtra | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!user || !movieId) return;
    let cancelled = false;

    async function load() {
      setLoading(true);
      setError(null);

      try {
        const movieRes = await api.get<Movie>(`/api/movies/${movieId}`);

        if (cancelled) return;
        setMovie(movieRes);

        // Détails TMDB complémentaires (backdrop, cast avec photos, tagline)
        if (movieRes?.tmdbId) {
          const tmdbExtra = await api
            .get<TmdbExtra>(`/api/tmdb/movie/${movieRes.tmdbId}`)
            .catch(() => null);
          if (!cancelled) setExtra(tmdbExtra);
        }
      } catch (err: unknown) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : "Erreur de chargement");
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    load();
    return () => {
      cancelled = true;
    };
  }, [movieId, user]);

  return { movie, extra, loading, error };
}
