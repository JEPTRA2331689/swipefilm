"use client";

import { useEffect, useState, useCallback } from "react";
import { api } from "@/lib/api";
import { useAppStore } from "@/lib/store";

export interface WatchlistMovie {
  id: string;
  tmdbId: number;
  title: string;
  posterPath: string | null;
  overview: string | null;
  tmdbRating: number;
  runtimeMinutes: number | null;
  genres: string[];
  releaseDate: string | null;
  contentType: string;
  swipedAt: string;
  isAvailable: boolean;
}

export function useWatchlist() {
  const user = useAppStore((s) => s.user);
  const [movies, setMovies] = useState<WatchlistMovie[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!user) return;
    setLoading(true);
    setError(null);
    try {
      const data = await api.get<WatchlistMovie[]>("/api/watchlist");
      // Déduplique par id au cas où le backend renvoie des entrées multiples
      const seen = new Set<string>();
      setMovies(
        data.filter((m) => (seen.has(m.id) ? false : (seen.add(m.id), true))),
      );
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Erreur de chargement");
    } finally {
      setLoading(false);
    }
  }, [user]);

  useEffect(() => {
    load();
  }, [load]);

  const remove = useCallback(async (movieId: string) => {
    try {
      await api.delete(`/api/watchlist/${movieId}`);
      setMovies((prev) => prev.filter((m) => m.id !== movieId));
    } catch {
      // silencieux
    }
  }, []);

  return { movies, loading, error, reload: load, remove };
}
