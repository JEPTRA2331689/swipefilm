"use client";

import { useEffect, useRef, useState, useCallback } from "react";
import { api } from "@/lib/api";
import type {
  HomeSection,
  AvailabilityFilter,
  ContentTypeFilter,
} from "@/types";

interface HomeSectionsResponse {
  sections: HomeSection[];
  page: number;
  pageSize: number;
  hasMore: boolean;
}

const CACHE_PREFIX = "swipefilm_home_";
const CACHE_TTL = 5 * 60 * 1000;
const PAGE_SIZE = 5;

function getCached(key: string): HomeSection[] | null {
  try {
    const raw = localStorage.getItem(CACHE_PREFIX + key);
    if (!raw) return null;
    const { data, ts } = JSON.parse(raw);
    if (Date.now() - ts > CACHE_TTL) return null;
    return data;
  } catch {
    return null;
  }
}

function setCache(key: string, data: HomeSection[]) {
  try {
    localStorage.setItem(
      CACHE_PREFIX + key,
      JSON.stringify({ data, ts: Date.now() }),
    );
  } catch {}
}

export function useHomeSections(
  enabled: boolean,
  availability: AvailabilityFilter = 0,
  contentType: ContentTypeFilter = 0,
  countPerSection = 20,
) {
  const [sections, setSections] = useState<HomeSection[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [hasMore, setHasMore] = useState(true);

  // Verrou pour éviter les doubles-fetch si le scroll est rapide
  const fetchingRef = useRef(false);
  const pageRef = useRef(1);

  // ✅ Accumulé côté client au fil du scroll, jamais persisté — un refresh
  // de page vide cette liste, donc "Pour vous" peut remontrer les mêmes
  // meilleurs résultats plutôt que de les épuiser au fil des rechargements.
  const shownIdsRef = useRef<Set<number>>(new Set());

  const cacheKey = enabled
    ? `${availability}_${contentType}_${countPerSection}`
    : null;
  const fetchPage = useCallback(
    async (page: number, isFirst: boolean) => {
      if (!enabled || fetchingRef.current) return;
      fetchingRef.current = true;
      if (isFirst) setLoading(true);
      else setLoadingMore(true);
      setError(null);

      try {
        let url = `/api/recommendations/home?page=${page}&pageSize=${PAGE_SIZE}&countPerSection=${countPerSection}&availability=${availability}`;
        if (contentType !== 0) url += `&contentType=${contentType}`;
        if (shownIdsRef.current.size > 0)
          url += `&excludeIds=${[...shownIdsRef.current].join(",")}`;
        const res = await api.get<HomeSectionsResponse>(url);

        setHasMore(res.hasMore);
        pageRef.current = page;

        for (const section of res.sections) {
          for (const movie of section.movies) {
            shownIdsRef.current.add(movie.tmdbId);
          }
        }

        setSections((prev) =>
          isFirst ? res.sections : [...prev, ...res.sections],
        );

        if (isFirst && cacheKey) setCache(cacheKey, res.sections);

        // Cas limite : page vide mais hasMore → enchaîne immédiatement la suivante
        if (res.hasMore && res.sections.length === 0) {
          fetchingRef.current = false;
          fetchPage(page + 1, false);
          return;
        }
      } catch (err) {
        if (isFirst)
          setError(err instanceof Error ? err.message : "Erreur de chargement");
      } finally {
        fetchingRef.current = false;
        if (isFirst) setLoading(false);
        else setLoadingMore(false);
      }
    },
    [enabled, availability, countPerSection, cacheKey],
  );

  // Reset et premier chargement quand les paramètres changent
  useEffect(() => {
    if (!enabled) return;

    setSections([]);
    setHasMore(true);
    setError(null);
    pageRef.current = 1;
    fetchingRef.current = false;
    shownIdsRef.current = new Set();

    if (cacheKey) {
      const cached = getCached(cacheKey);
      if (cached) {
        setSections(cached);
        setLoading(false);
      }
    }

    fetchPage(1, true);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [enabled, availability, contentType, countPerSection]);

  const loadMore = useCallback(() => {
    if (!hasMore || fetchingRef.current || loadingMore) return;
    fetchPage(pageRef.current + 1, false);
  }, [hasMore, loadingMore, fetchPage]);

  return { sections, loading, loadingMore, error, hasMore, loadMore };
}
