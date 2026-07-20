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
  similar: Movie[];
  extra: TmdbExtra | null;
  loading: boolean;
  error: string | null;
}

export function useMovieDetail(movieId: string): MovieDetailData {
  const activeServer = useAppStore((s) => s.activeServer);
  const [movie, setMovie] = useState<Movie | null>(null);
  const [similar, setSimilar] = useState<Movie[]>([]);
  const [extra, setExtra] = useState<TmdbExtra | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!activeServer?.id || !movieId) return;
    let cancelled = false;

    async function load() {
      setLoading(true);
      setError(null);

      try {
        // Dans useMovieDetail.ts — remplace les deux appels actuels par :

        const [movieRes, simRes] = await Promise.all([
        // ✅ Vrai endpoint film par ID
        api.get<Movie>(`/api/movies/${movieId}`),

        // Films similaires — garde celui-là
        api.get<Movie[]>(
            `/api/recommendations/${activeServer!.id}` +
            `?section=because_you_liked&basedOnMovieId=${movieId}&count=12`
        ),
        ]);

        setMovie(movieRes);
        setSimilar(simRes ?? []);

        // Détails TMDB complémentaires (backdrop, cast avec photos)
        if (movieRes?.tmdbId) {
        const tmdbExtra = await fetch(`/api/tmdb/movie/${movieRes.tmdbId}`)
            .then((r) => r.json())
            .catch(() => null);
        if (!cancelled) setExtra(tmdbExtra);
        }

        if (cancelled) return;

        // Le film de référence est dans refRes[0] ou le premier de simRes
        const foundMovie = movieRes ?? simRes?.[0] ?? null;
        setMovie(foundMovie);
        setSimilar(simRes?.slice(0, 12) ?? []);

        // 2. Détails TMDB (cast, directors, backdrop) si on a le tmdbId
        const tmdbId = foundMovie?.tmdbId;
        if (tmdbId) {
          const tmdbExtra = await fetch(`/api/tmdb/movie/${tmdbId}`)
            .then((r) => r.json())
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
    return () => { cancelled = true; };
  }, [movieId, activeServer?.id]);

  return { movie, similar, extra, loading, error };
}