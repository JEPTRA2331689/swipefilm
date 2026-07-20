"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import type { HomeSection, AvailabilityFilter } from "@/types";

export function useHomeSections(
  serverId: string | null,
  availability: AvailabilityFilter = "All",
  countPerSection = 20
) {
  const [sections, setSections] = useState<HomeSection[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!serverId) return;

    let cancelled = false;
    setLoading(true);
    setError(null);

    api
      .get<HomeSection[]>(
        `/api/recommendations/home/${serverId}?countPerSection=${countPerSection}&availability=${availability}`
      )
      .then((data) => {
        if (!cancelled) setSections(data);
      })
      .catch((err) => {
        if (!cancelled) setError(err.message ?? "Erreur de chargement");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [serverId, availability, countPerSection]);

  return { sections, loading, error };
}
