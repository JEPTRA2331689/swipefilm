"use client";

import { useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { motion, AnimatePresence } from "framer-motion";
import { TopNav } from "@/components/layout/TopNav";
import { useWatchlist, type WatchlistMovie } from "@/hooks/useWatchlist";
import { api } from "@/lib/api";
import { cn, tmdbImage, releaseYear, formatRuntime } from "@/lib/utils";

type SortKey = "date" | "rating" | "title";
type FilterKey = "all" | "available" | "unavailable";

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

export default function ListPage() {
  const { movies, loading, error, remove } = useWatchlist();
  const [sort, setSort] = useState<SortKey>("date");
  const [filter, setFilter] = useState<FilterKey>("all");
  const [removingId, setRemovingId] = useState<string | null>(null);

  const filtered = sortMovies(
    movies.filter((m) => {
      if (filter === "available") return m.isAvailable;
      if (filter === "unavailable") return !m.isAvailable;
      return true;
    }),
    sort
  );

  const available = filtered.filter((m) => m.isAvailable);
  const unavailable = filtered.filter((m) => !m.isAvailable);

  async function handleRemove(movie: WatchlistMovie) {
    setRemovingId(movie.id);
    await remove(movie.id);
    setRemovingId(null);
  }

  async function handleDislike(movie: WatchlistMovie) {
    setRemovingId(movie.id);
    try {
      await api.post("/api/swipes", {
        movieId: movie.id,
        direction: 0,
        durationMs: 2000,
        context: 0,
      });
      await remove(movie.id);
    } finally {
      setRemovingId(null);
    }
  }

  return (
    <div className="min-h-screen">
      <TopNav />

      <div className="px-4 pt-10 pb-4 md:px-12">
        <h1 className="font-display text-3xl md:text-4xl font-semibold text-text-primary">
          Ma liste
        </h1>
        <p className="mt-1 text-sm text-text-secondary">
          {movies.length} film{movies.length !== 1 ? "s" : ""} likés
        </p>

        <div className="mt-4 flex flex-wrap gap-2 items-center">
          <div className="flex rounded-pill border border-border overflow-hidden">
            {(["all", "available", "unavailable"] as FilterKey[]).map((f) => (
              <button
                key={f}
                onClick={() => setFilter(f)}
                className={cn(
                  "px-3 py-1.5 text-xs font-medium transition-colors border-r border-border last:border-r-0",
                  filter === f
                    ? "bg-accent/15 text-accent"
                    : "bg-surface text-text-secondary hover:text-text-primary"
                )}
              >
                {f === "all" ? "Tous" : f === "available" ? "🟢 Dispo" : "🟡 À requêter"}
              </button>
            ))}
          </div>

          <select
            value={sort}
            onChange={(e) => setSort(e.target.value as SortKey)}
            className="rounded-input border border-border bg-surface px-3 py-1.5 text-xs text-text-primary outline-none focus:border-accent"
          >
            {SORT_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>
        </div>
      </div>

      {loading && (
        <div className="flex justify-center py-24">
          <div className="h-10 w-10 rounded-full border-2 border-accent border-t-transparent animate-spin" />
        </div>
      )}

      {error && (
        <p className="px-4 py-12 text-center text-sm text-error md:px-12">{error}</p>
      )}

      {!loading && !error && movies.length === 0 && (
        <div className="flex flex-col items-center justify-center py-24 gap-3 text-text-secondary">
          <p className="text-4xl">🎬</p>
          <p className="text-sm">Aucun film liké pour l&apos;instant.</p>
          <Link href="/swipe" className="text-accent text-sm hover:underline">
            Commencer à swiper →
          </Link>
        </div>
      )}

      {!loading && !error && movies.length > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-0 border-t border-border">
          <div className="border-b border-border md:border-b-0 md:border-r">
            <div className="flex items-center gap-2 px-4 py-3 md:px-8 border-b border-border bg-surface/30">
              <div className="h-2.5 w-2.5 rounded-full bg-swipe-star" />
              <h2 className="text-sm font-medium text-text-primary">Disponibles</h2>
              <span className="ml-auto text-xs text-text-secondary">{available.length}</span>
            </div>
            <div className="divide-y divide-border">
              <AnimatePresence>
                {available.map((movie) => (
                  <MovieRow
                    key={movie.id}
                    movie={movie}
                    removing={removingId === movie.id}
                    onRemove={() => handleRemove(movie)}
                    onDislike={() => handleDislike(movie)}
                  />
                ))}
              </AnimatePresence>
              {available.length === 0 && (
                <p className="px-4 py-8 text-center text-xs text-text-secondary">Aucun film disponible</p>
              )}
            </div>
          </div>

          <div>
            <div className="flex items-center gap-2 px-4 py-3 md:px-8 border-b border-border bg-surface/30">
              <div className="h-2.5 w-2.5 rounded-full bg-text-secondary/40" />
              <h2 className="text-sm font-medium text-text-primary">À requêter</h2>
              <span className="ml-auto text-xs text-text-secondary">{unavailable.length}</span>
            </div>
            <div className="divide-y divide-border">
              <AnimatePresence>
                {unavailable.map((movie) => (
                  <MovieRow
                    key={movie.id}
                    movie={movie}
                    removing={removingId === movie.id}
                    onRemove={() => handleRemove(movie)}
                    onDislike={() => handleDislike(movie)}
                    showRequest
                  />
                ))}
              </AnimatePresence>
              {unavailable.length === 0 && (
                <p className="px-4 py-8 text-center text-xs text-text-secondary">Aucun film à requêter</p>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function MovieRow({
  movie,
  removing,
  onRemove,
  onDislike,
  showRequest = false,
}: {
  movie: WatchlistMovie;
  removing: boolean;
  onRemove: () => void;
  onDislike: () => void;
  showRequest?: boolean;
}) {
  const [requested, setRequested] = useState(false);
  const poster = tmdbImage(movie.posterPath, "w185");

  async function handleRequest() {
    try {
      await api.post(`/api/seerr/request/${movie.tmdbId}`);
      setRequested(true);
    } catch { /* silencieux */ }
  }

  return (
    <motion.div
      layout
      initial={{ opacity: 0, y: -8 }}
      animate={{ opacity: removing ? 0 : 1, y: 0 }}
      exit={{ opacity: 0, height: 0, overflow: "hidden" }}
      transition={{ duration: 0.25 }}
      className="flex gap-3 px-4 py-3 hover:bg-surface/40 transition-colors md:px-8"
    >
      <Link href={`/movie/${movie.id}`} className="flex-shrink-0">
        <div className="relative w-12 aspect-[2/3] rounded-md overflow-hidden bg-surface-alt">
          {poster ? (
            <Image src={poster} alt={movie.title} fill sizes="48px" className="object-cover" />
          ) : (
            <div className="flex h-full items-center justify-center text-[10px] text-text-secondary p-1 text-center">
              {movie.title}
            </div>
          )}
        </div>
      </Link>

      <div className="flex-1 min-w-0">
        <Link href={`/movie/${movie.id}`}>
          <p className="text-sm font-medium text-text-primary line-clamp-1 hover:text-accent transition-colors">
            {movie.title}
          </p>
        </Link>
        <div className="flex flex-wrap items-center gap-1.5 mt-0.5">
          {releaseYear(movie.releaseDate) && (
            <span className="text-[11px] text-text-secondary">{releaseYear(movie.releaseDate)}</span>
          )}
          {movie.runtimeMinutes && (
            <span className="text-[11px] text-text-secondary">· {formatRuntime(movie.runtimeMinutes)}</span>
          )}
          <span className="text-[11px] text-warning">★ {movie.tmdbRating.toFixed(1)}</span>
        </div>
        {movie.genres?.length > 0 && (
          <div className="flex flex-wrap gap-1 mt-1">
            {movie.genres.slice(0, 2).map((g) => (
              <span key={g} className="rounded-pill bg-accent/10 border border-accent/20 px-1.5 py-0.5 text-[10px] text-accent">
                {g}
              </span>
            ))}
          </div>
        )}
      </div>

      <div className="flex flex-col items-end gap-1.5 flex-shrink-0">
        {showRequest && !requested && (
          <button onClick={handleRequest} className="rounded-pill border border-border bg-surface px-2 py-1 text-[10px] text-text-secondary hover:text-accent hover:border-accent/40 transition-colors">
            📥 Requêter
          </button>
        )}
        {requested && <span className="text-[10px] text-success">✓ Demandé</span>}
        <button onClick={onDislike} disabled={removing} className="rounded-pill border border-border bg-surface px-2 py-1 text-[10px] text-text-secondary hover:text-swipe-skip hover:border-swipe-skip/40 transition-colors">
          👎 Retirer
        </button>
        <button onClick={onRemove} disabled={removing} className="text-[10px] text-text-secondary/50 hover:text-error transition-colors">
          ✕
        </button>
      </div>
    </motion.div>
  );
}