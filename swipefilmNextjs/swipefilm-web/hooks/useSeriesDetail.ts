"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { useAppStore } from "@/lib/store";
import type { Series } from "@/types";

interface SeriesDetailData {
  series: Series | null;
  loading: boolean;
  error: string | null;
}

// ✅ Contrairement à useMovieDetail, un seul appel suffit — GET /api/series/{id}
// renvoie déjà cast/créateurs/certification/backdrop en direct depuis TMDB,
// pas besoin d'un second appel de type /api/tmdb/tv/{id}.
export function useSeriesDetail(seriesId: string): SeriesDetailData {
  const user = useAppStore((s) => s.user);
  const [series, setSeries] = useState<Series | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!user || !seriesId) return;
    let cancelled = false;

    async function load() {
      setLoading(true);
      setError(null);

      try {
        const res = await api.get<Series>(`/api/series/${seriesId}`);
        if (!cancelled) setSeries(res);
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
  }, [seriesId, user]);

  return { series, loading, error };
}
