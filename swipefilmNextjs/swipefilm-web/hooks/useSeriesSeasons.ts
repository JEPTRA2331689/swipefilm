"use client";

import { useCallback, useEffect, useState } from "react";
import { api } from "@/lib/api";
import { useAppStore } from "@/lib/store";
import type { SeriesSeason } from "@/types";

interface SeriesSeasonsData {
  seasons: SeriesSeason[];
  loading: boolean;
  error: string | null;
  refresh: () => void;
}

// GET /api/series/{id}/seasons — disponibilité et statut de demande par saison.
export function useSeriesSeasons(seriesId: string | null): SeriesSeasonsData {
  const user = useAppStore((s) => s.user);
  const [seasons, setSeasons] = useState<SeriesSeason[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    if (!user || !seriesId) return;
    let cancelled = false;

    async function load() {
      setLoading(true);
      setError(null);

      try {
        const res = await api.get<SeriesSeason[]>(
          `/api/series/${seriesId}/seasons`,
        );
        if (!cancelled) setSeasons(res);
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
  }, [seriesId, user, reloadKey]);

  const refresh = useCallback(() => setReloadKey((k) => k + 1), []);

  return { seasons, loading, error, refresh };
}
