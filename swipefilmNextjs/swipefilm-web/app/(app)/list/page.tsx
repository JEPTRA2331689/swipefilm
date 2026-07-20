"use client";

import { useState, useEffect, useCallback } from "react";
import Link from "next/link";
import { TopNav } from "@/components/layout/TopNav";
import { useWatchlist, type WatchlistMovie } from "@/hooks/useWatchlist";
import { MovieRequestCard } from "@/components/movie/MovieRequestCard";
import {
  createRequest,
  cancelRequest,
  getRequests,
  requestMedia,
  type MovieRequest,
  type RequestStatusCode,
} from "@/lib/auth";
import { useAuth } from "@/context/AuthContext";
import { Permission } from "@/lib/permissions";
import { cn } from "@/lib/utils";
import { Button, IconButton } from "@/components/ui/Button";

type SortKey = "date" | "rating" | "title";
type FilterKey = "all" | "available" | "unavailable";

const PAGE_SIZE = 10;

const FILTERS: { value: FilterKey; label: string }[] = [
  { value: "all", label: "Tous" },
  { value: "available", label: "Disponibles" },
  { value: "unavailable", label: "À requêter" },
];

const SORT_OPTIONS: { value: SortKey; label: string }[] = [
  { value: "date", label: "Plus récents" },
  { value: "rating", label: "Mieux notés" },
  { value: "title", label: "Titre A→Z" },
];

function sortMovies(movies: WatchlistMovie[], sort: SortKey) {
  return [...movies].sort((a, b) => {
    if (sort === "rating") return b.tmdbRating - a.tmdbRating;
    if (sort === "title") return a.title.localeCompare(b.title);
    return new Date(b.swipedAt).getTime() - new Date(a.swipedAt).getTime();
  });
}

// Construit un MovieRequest "fake" pour les films/séries sans requête active
function toFakeRequest(
  movie: WatchlistMovie,
  statusCode: RequestStatusCode = -1,
): MovieRequest {
  const isSeries = movie.contentType === "series";
  return {
    id: movie.id,
    status: statusCode,
    type: isSeries ? 1 : 0,
    requestedAt: movie.swipedAt,
    processedAt: null,
    availableAt: null,
    declineReason: null,
    message: null,
    externalId: null,
    movie: isSeries
      ? null
      : {
          id: movie.id,
          tmdbId: movie.tmdbId,
          title: movie.title,
          posterPath: movie.posterPath,
          tmdbRating: movie.tmdbRating,
          contentType: movie.contentType,
        },
    series: isSeries
      ? {
          id: movie.id,
          tmdbId: movie.tmdbId,
          title: movie.title,
          posterPath: movie.posterPath,
          tmdbRating: movie.tmdbRating,
        }
      : null,
    requestedBy: { id: "", displayName: "" },
    processedBy: null,
  };
}

export default function ListPage() {
  const { movies, loading, error, remove } = useWatchlist();
  const { hasPermission } = useAuth();
  const canRequest = hasPermission(Permission.CanRequest);

  const [filter, setFilter] = useState<FilterKey>("all");
  const [sort, setSort] = useState<SortKey>("date");
  const [page, setPage] = useState(1);

  // tmdbId → vraie requête backend (si elle existe)
  const [requestMap, setRequestMap] = useState<Record<number, MovieRequest>>(
    {},
  );
  // movieId → "requesting" (en cours d'envoi)
  const [submitting, setSubmitting] = useState<Set<string>>(new Set());

  // Chargement initial des requêtes existantes
  useEffect(() => {
    getRequests()
      .then((list) => {
        const map: Record<number, MovieRequest> = {};
        for (const r of list) map[requestMedia(r).tmdbId] = r;
        setRequestMap(map);
      })
      .catch(() => {
        /* silencieux */
      });
  }, []);

  // Filtrage + tri
  const filtered = sortMovies(
    movies.filter((m) => {
      if (filter === "available") return m.isAvailable;
      if (filter === "unavailable") return !m.isAvailable;
      return true;
    }),
    sort,
  );

  // Pagination
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const safePage = Math.min(page, totalPages);
  const pageMovies = filtered.slice(
    (safePage - 1) * PAGE_SIZE,
    safePage * PAGE_SIZE,
  );

  function goTo(p: number) {
    setPage(Math.max(1, Math.min(p, totalPages)));
  }
  function changeFilter(f: FilterKey) {
    setFilter(f);
    setPage(1);
  }

  const handleRequest = useCallback(async (movie: WatchlistMovie) => {
    setSubmitting((s) => new Set(s).add(movie.id));
    try {
      const isSeries = movie.contentType === "series";
      const created = await createRequest(
        isSeries ? { seriesId: movie.id } : { movieId: movie.id },
      );
      setRequestMap((prev) => ({ ...prev, [movie.tmdbId]: created }));
    } catch {
      /* silencieux */
    } finally {
      setSubmitting((s) => {
        const n = new Set(s);
        n.delete(movie.id);
        return n;
      });
    }
  }, []);

  const handleCancel = useCallback(async (requestId: string) => {
    try {
      await cancelRequest(requestId);
      // ✅ cancelRequest supprime la requête côté backend — plus aucune
      // requête active, on retombe sur le sentinel "none" (-1)
      setRequestMap((prev) => {
        const entry = Object.values(prev).find((r) => r.id === requestId);
        if (!entry) return prev;
        return {
          ...prev,
          [requestMedia(entry).tmdbId]: {
            ...entry,
            status: -1 as RequestStatusCode,
          },
        };
      });
    } catch {
      /* silencieux */
    }
  }, []);

  const handleRemove = useCallback(
    async (movieId: string) => {
      await remove(movieId);
    },
    [remove],
  );

  return (
    <div className="bg-bg-primary min-h-screen">
      <TopNav />

      <div className="mx-auto max-w-2xl px-4 pt-8 pb-16">
        {/* ── Titre ── */}
        <div className="mb-6">
          <h1 className="font-display text-text-primary text-2xl font-bold">
            Ma liste
          </h1>
          <p className="text-text-secondary mt-1 text-sm">
            {movies.length} titre{movies.length !== 1 ? "s" : ""} likés
          </p>
        </div>

        {/* ── Filtres de disponibilité ── */}
        <div className="mb-1 flex gap-2">
          {FILTERS.map((f) => {
            const count =
              f.value === "all"
                ? movies.length
                : f.value === "available"
                  ? movies.filter((m) => m.isAvailable).length
                  : movies.filter((m) => !m.isAvailable).length;
            return (
              <Button
                key={f.value}
                variant={filter === f.value ? "primary" : "secondary"}
                size="sm"
                className={cn(
                  "gap-1.5 rounded-lg",
                  filter === f.value &&
                    "bg-accent text-white shadow-sm hover:brightness-110",
                )}
                onClick={() => changeFilter(f.value)}
              >
                {f.label}
                <span
                  className={cn(
                    "rounded-full px-1.5 py-0.5 text-[11px] font-semibold",
                    filter === f.value ? "bg-white/20" : "bg-surface-alt",
                  )}
                >
                  {count}
                </span>
              </Button>
            );
          })}
        </div>

        {/* ── Séparateur ── */}
        <div className="border-border my-4 border-t" />

        {/* ── Tri ── */}
        <div className="mb-6 flex items-center gap-2">
          <span className="text-text-secondary text-xs">Trier</span>
          <div className="flex gap-1">
            {SORT_OPTIONS.map((o) => (
              <Button
                key={o.value}
                variant={sort === o.value ? "accent-soft" : "secondary"}
                size="sm"
                className="rounded-lg px-3 py-1.5 text-xs"
                onClick={() => setSort(o.value)}
              >
                {o.label}
              </Button>
            ))}
          </div>
        </div>

        {/* ── État chargement / erreur / vide ── */}
        {loading && (
          <div className="flex justify-center py-20">
            <span className="border-accent h-8 w-8 animate-spin rounded-full border-2 border-t-transparent" />
          </div>
        )}

        {error && (
          <p className="py-12 text-center text-sm text-red-400">{error}</p>
        )}

        {!loading && !error && movies.length === 0 && (
          <div className="text-text-secondary flex flex-col items-center justify-center gap-3 py-20">
            <i className="ph-thin ph-film-strip text-5xl" />
            <p className="text-sm">Aucun film liké pour l&apos;instant.</p>
            <Link href="/swipe" className="text-accent text-sm hover:underline">
              Commencer à swiper →
            </Link>
          </div>
        )}

        {!loading && !error && filtered.length === 0 && movies.length > 0 && (
          <div className="text-text-secondary flex flex-col items-center justify-center gap-2 py-16">
            <i className="ph-thin ph-funnel text-4xl" />
            <p className="text-sm">Aucun film pour ce filtre.</p>
          </div>
        )}

        {/* ── Liste des films ── */}
        {!loading && !error && pageMovies.length > 0 && (
          <div className="space-y-3">
            {pageMovies.map((movie) => {
              const realRequest = requestMap[movie.tmdbId] ?? null;
              const isSubmitting = submitting.has(movie.id);

              // Statut réel : requête existante > disponible > pas de requête
              // status: 0=Pending 1=Approved 2=Downloading 3=PartiallyAvailable 4=Available 5=Declined
              let request: MovieRequest;
              let canShowRequest: boolean;

              if (realRequest && realRequest.status !== 5) {
                // Requête active (tout sauf refusée) — on utilise les vraies données
                request = realRequest;
                canShowRequest = false;
              } else if (movie.isAvailable) {
                // Dispo sur le serveur média
                request = toFakeRequest(movie, 4);
                canShowRequest = false;
              } else {
                // Pas de requête active → peut requêter
                request = toFakeRequest(movie);
                canShowRequest = canRequest && !isSubmitting;
              }

              return (
                <div key={movie.id} className="group relative">
                  <MovieRequestCard
                    request={request}
                    requesting={isSubmitting}
                    onRequest={
                      canShowRequest ? () => handleRequest(movie) : undefined
                    }
                    onCancel={
                      realRequest?.status === 0 ? handleCancel : undefined
                    }
                  />
                  {/* Bouton retirer (discret, apparaît au hover) */}
                  <button
                    onClick={() => handleRemove(movie.id)}
                    className="bg-surface-alt border-border text-text-secondary/50 absolute top-2 right-2 flex h-6 w-6 cursor-pointer items-center justify-center rounded-full border opacity-0 transition-all group-hover:opacity-100 hover:border-red-500/30 hover:text-red-400"
                    title="Retirer de la liste"
                  >
                    <i className="ph-thin ph-x text-xs" />
                  </button>
                </div>
              );
            })}
          </div>
        )}

        {/* ── Pagination ── */}
        {totalPages > 1 && (
          <div className="mt-8 flex items-center justify-center gap-3">
            <IconButton
              variant="neutral"
              className="hover:border-accent/40 h-9 w-9 rounded-lg"
              onClick={() => goTo(safePage - 1)}
              disabled={safePage === 1}
            >
              <i className="ph-thin ph-caret-left text-base" />
            </IconButton>

            {/* Numéros de page */}
            {Array.from({ length: totalPages }, (_, i) => i + 1).map((p) => {
              const isActive = p === safePage;
              const isNear = Math.abs(p - safePage) <= 2;
              if (!isNear && p !== 1 && p !== totalPages) {
                if (p === safePage - 3 || p === safePage + 3) {
                  return (
                    <span key={p} className="text-text-secondary/40 text-sm">
                      …
                    </span>
                  );
                }
                return null;
              }
              return (
                <IconButton
                  key={p}
                  variant="neutral"
                  className={cn(
                    "hover:border-accent/40 h-9 w-9 rounded-lg text-sm font-medium",
                    isActive &&
                      "bg-accent border-accent hover:bg-accent text-white",
                  )}
                  onClick={() => goTo(p)}
                >
                  {p}
                </IconButton>
              );
            })}

            <IconButton
              variant="neutral"
              className="hover:border-accent/40 h-9 w-9 rounded-lg"
              onClick={() => goTo(safePage + 1)}
              disabled={safePage === totalPages}
            >
              <i className="ph-thin ph-caret-right text-base" />
            </IconButton>
          </div>
        )}

        {/* Info pagination */}
        {filtered.length > 0 && (
          <p className="text-text-secondary/50 mt-3 text-center text-xs">
            {(safePage - 1) * PAGE_SIZE + 1}–
            {Math.min(safePage * PAGE_SIZE, filtered.length)} sur{" "}
            {filtered.length} films
          </p>
        )}
      </div>
    </div>
  );
}
