"use client";

import { useState, useEffect, useCallback, useRef } from "react";
import { TopNav } from "@/components/layout/TopNav";
import { VerticalPosterCarousel } from "@/components/onboarding/VerticalPosterCarousel";
import { Button } from "@/components/ui/Button";
import { api } from "@/lib/api";
import { useAppStore } from "@/lib/store";
import { cn } from "@/lib/utils";
import type { AvailabilityFilter, Movie } from "@/types";

const AVAILABILITY_OPTIONS: { value: AvailabilityFilter; label: string; emoji: string }[] = [
  { value: "All", label: "Mix", emoji: "🔀" },
  { value: "AvailableOnly", label: "Disponibles", emoji: "🟢" },
  { value: "UnavailableOnly", label: "À découvrir", emoji: "🟡" },
];

const AUTO_UPDATE_AFTER = 10; // swipes avant mise à jour profil auto

export default function SwipePage() {
  const activeServer = useAppStore((s) => s.activeServer);
  const [availability, setAvailability] = useState<AvailabilityFilter>("All");
  const [movies, setMovies] = useState<Movie[]>([]);
  const [activeIndex, setActiveIndex] = useState(0);
  const [loading, setLoading] = useState(true);
  const [swipeCount, setSwipeCount] = useState(0);
  const [lastAction, setLastAction] = useState<"like" | "dislike" | null>(null);
  const [profileUpdated, setProfileUpdated] = useState(false);
  const seenIds = useRef<Set<string>>(new Set());
  const swipeStartTime = useRef<number>(Date.now());

  const currentMovie = movies[activeIndex] ?? null;

  // ── Chargement des films ──────────────────────────────────────
  const loadMovies = useCallback(async () => {
    if (!activeServer?.id) return;
    setLoading(true);
    try {
      const excludeParam = seenIds.current.size > 0
        ? `&excludeIds=${[...seenIds.current].join(",")}`
        : "";

      const data = await api.get<Movie[]>(
        `/api/recommendations/${activeServer.id}` +
        `?section=surprise_me&count=20&availability=${availability}${excludeParam}`
      );
      setMovies(data);
      setActiveIndex(0);
    } catch {
      // silencieux
    } finally {
      setLoading(false);
    }
  }, [activeServer?.id, availability]);

  useEffect(() => {
    seenIds.current.clear();
    setSwipeCount(0);
    loadMovies();
  }, [availability, loadMovies]);

  // ── Swipe ─────────────────────────────────────────────────────
  const handleSwipe = useCallback(async (
    movie: Movie,
    direction: "Left" | "Right"
  ) => {
    const durationMs = Date.now() - swipeStartTime.current;
    swipeStartTime.current = Date.now();

    setLastAction(direction === "Right" ? "like" : "dislike");
    setTimeout(() => setLastAction(null), 600);

    // Marque comme vu
    seenIds.current.add(String(movie.tmdbId));

    // Avance dans la liste
    setActiveIndex((i) => {
      const next = i + 1;
      // Recharge si on approche de la fin
      if (next >= movies.length - 3) loadMovies();
      return next % movies.length;
    });

    const newCount = swipeCount + 1;
    setSwipeCount(newCount);

    // Appel API swipe
    try {
      await api.post("/api/swipes", {
        movieId: movie.id,
        direction: direction === "Right" ? 1 : 0,
        durationMs,
        context: 0,
      });
    } catch { /* silencieux */ }

    // Mise à jour profil auto
    if (newCount % AUTO_UPDATE_AFTER === 0) {
      try {
        await api.post("/api/recommendations/profile/update");
        setProfileUpdated(true);
        setTimeout(() => setProfileUpdated(false), 2500);
      } catch { /* silencieux */ }
    }
  }, [movies, swipeCount, loadMovies]);

  // ── Raccourcis clavier ────────────────────────────────────────
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (!currentMovie) return;
      if (e.key === "ArrowRight" || e.key === "l" || e.key === "L") {
        handleSwipe(currentMovie, "Right");
      }
      if (e.key === "ArrowLeft" || e.key === "j" || e.key === "J") {
        handleSwipe(currentMovie, "Left");
      }
    }
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [currentMovie, handleSwipe]);

  return (
    <div className="flex flex-col h-screen overflow-hidden">
      <TopNav />

      {/* ── Filtre disponibilité ── */}
      <div className="flex items-center justify-between px-4 py-3 md:px-12 border-b border-border flex-shrink-0">
        <div className="flex gap-2">
          {AVAILABILITY_OPTIONS.map((opt) => (
            <button
              key={opt.value}
              onClick={() => setAvailability(opt.value)}
              className={cn(
                "rounded-pill border px-3 py-1 text-xs font-medium transition-all",
                availability === opt.value
                  ? "border-accent bg-accent/15 text-accent shadow-[0_0_10px_rgba(192,132,252,0.2)]"
                  : "border-border text-text-secondary hover:text-text-primary"
              )}
            >
              {opt.emoji} {opt.label}
            </button>
          ))}
        </div>

        <div className="flex items-center gap-3">
          {/* Badge profil mis à jour */}
          {profileUpdated && (
            <span className="text-xs text-success animate-pulse">
              ✓ Profil mis à jour
            </span>
          )}
          {/* Compteur swipes */}
          <span className="text-xs text-text-secondary">
            {swipeCount} swipe{swipeCount !== 1 ? "s" : ""}
          </span>
        </div>
      </div>

      {/* ── Zone principale ── */}
      <div className="flex-1 flex flex-col min-h-0">

        {/* Feedback like/dislike */}
        {lastAction && (
          <div className={cn(
            "absolute inset-0 pointer-events-none z-20 flex items-center justify-center transition-opacity duration-300",
            lastAction === "like"
              ? "bg-swipe-like/8"
              : "bg-swipe-skip/8"
          )}>
            <span className="text-7xl opacity-80 drop-shadow-lg">
              {lastAction === "like" ? "👍" : "👎"}
            </span>
          </div>
        )}

        {/* Carrousel */}
        <div className="flex-1 min-h-0">
          {loading ? (
            <div className="flex h-full items-center justify-center">
              <div className="h-10 w-10 rounded-full border-2 border-accent border-t-transparent animate-spin" />
            </div>
          ) : movies.length === 0 ? (
            <div className="flex h-full flex-col items-center justify-center gap-4 text-text-secondary">
              <p className="text-sm">Aucun film disponible pour ce filtre.</p>
              <Button variant="outline" onClick={loadMovies}>Réessayer</Button>
            </div>
          ) : (
            <VerticalPosterCarousel
              movies={movies}
              mode="swipe"
              onSwipe={handleSwipe}
              activeIndex={activeIndex}
            />
          )}
        </div>

        {/* ── Infos film central ── */}
        {currentMovie && !loading && (
          <div className="flex-shrink-0 px-4 pb-2 pt-1 text-center md:px-12">
            <p className="font-display text-lg font-semibold text-text-primary line-clamp-1">
              {currentMovie.title}
            </p>
            <div className="flex items-center justify-center gap-2 mt-0.5 text-xs text-text-secondary">
              {currentMovie.releaseDate && (
                <span>{currentMovie.releaseDate.slice(0, 4)}</span>
              )}
              <span>·</span>
              <span className="text-warning">★ {currentMovie.tmdbRating.toFixed(1)}</span>
              {currentMovie.genres?.slice(0, 2).map((g) => (
                <span key={g} className="rounded-pill bg-accent/10 border border-accent/20 px-2 py-0.5 text-accent text-[10px]">
                  {g}
                </span>
              ))}
            </div>
          </div>
        )}

        {/* ── Boutons Like / Dislike ── */}
        <div className="flex-shrink-0 flex items-center justify-center gap-6 px-4 pb-6 pt-3">
          <button
            onClick={() => currentMovie && handleSwipe(currentMovie, "Left")}
            disabled={!currentMovie || loading}
            className="flex flex-col items-center gap-1.5 group"
          >
            <div className="flex h-16 w-16 items-center justify-center rounded-full border-2 border-swipe-skip/40 bg-swipe-skip/10 text-2xl transition-all group-hover:border-swipe-skip group-hover:bg-swipe-skip/20 group-hover:scale-110 group-active:scale-95">
              👎
            </div>
            <span className="text-[11px] text-text-secondary group-hover:text-swipe-skip transition-colors">
              J / ←
            </span>
          </button>

          <button
            onClick={() => currentMovie && handleSwipe(currentMovie, "Right")}
            disabled={!currentMovie || loading}
            className="flex flex-col items-center gap-1.5 group"
          >
            <div className="flex h-16 w-16 items-center justify-center rounded-full border-2 border-swipe-like/40 bg-swipe-like/10 text-2xl transition-all group-hover:border-swipe-like group-hover:bg-swipe-like/20 group-hover:scale-110 group-active:scale-95">
              👍
            </div>
            <span className="text-[11px] text-text-secondary group-hover:text-swipe-like transition-colors">
              L / →
            </span>
          </button>
        </div>
      </div>
    </div>
  );
}