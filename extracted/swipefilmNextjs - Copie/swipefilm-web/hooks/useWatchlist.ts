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
  const activeServer = useAppStore((s) => s.activeServer);
  const [movies, setMovies] = useState<WatchlistMovie[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!activeServer?.id) return;
    setLoading(true);
    setError(null);
    try {
      const data = await api.get<WatchlistMovie[]>(
        `/api/watchlist/${activeServer.id}`
      );
      setMovies(data);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Erreur de chargement");
    } finally {
      setLoading(false);
    }
  }, [activeServer?.id]);

  useEffect(() => { load(); }, [load]);

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