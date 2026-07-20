"use client";

import { useState, useEffect, useCallback, useRef } from "react";
import { TopNav } from "@/components/layout/TopNav";
import { SwipeFocusRail } from "@/components/movie/SwipeFocusRail";
import { SwipeActionButtons } from "@/components/movie/SwipeActionButtons";
import { Button } from "@/components/ui/Button";
import { api } from "@/lib/api";
import { useAppStore } from "@/lib/store";
import { cn } from "@/lib/utils";
import type { AvailabilityFilter, Movie } from "@/types";

const AVAILABILITY_OPTIONS: {
  value: AvailabilityFilter;
  label: string;
  icon: string;
}[] = [
  { value: 0, label: "Mix", icon: "ph-thin ph-shuffle" },
  { value: 1, label: "Disponibles", icon: "ph-thin ph-check-circle" },
  { value: 2, label: "À découvrir", icon: "ph-thin ph-download-simple" },
];

const AUTO_UPDATE_AFTER = 3; // swipes avant mise à jour profil auto

export default function SwipePage() {
  const user = useAppStore((s) => s.user);
  const [availability, setAvailability] = useState<AvailabilityFilter>(0);
  const [movies, setMovies] = useState<Movie[]>([]);
  const [activeIndex, setActiveIndex] = useState(0);
  const [loading, setLoading] = useState(true);
  const [isTmdbFallback, setIsTmdbFallback] = useState(false);
  const [tmdbPage, setTmdbPage] = useState(1);
  const [swipeCount, setSwipeCount] = useState(0);
  const [lastAction, setLastAction] = useState<"like" | "dislike" | null>(null);
  const [profileUpdated, setProfileUpdated] = useState(false);
  const seenIds = useRef<Set<string>>(new Set());
  const swipeStartTime = useRef<number>(Date.now());

  const currentMovie = movies[activeIndex] ?? null;

  // ── Fallback TMDB popular ─────────────────────────────────────
  const loadTmdbPopular = useCallback(async (page = 1) => {
    const data = (await fetch(`/api/tmdb/popular?page=${page}`).then((r) =>
      r.json(),
    )) as Movie[];
    return data;
  }, []);

  // ── Chargement des films ──────────────────────────────────────
  const loadMovies = useCallback(async () => {
    if (!user) return;
    setLoading(true);
    try {
      const excludeParam =
        seenIds.current.size > 0
          ? `&excludeIds=${[...seenIds.current].join(",")}`
          : "";

      const data = await api.get<Movie[]>(
        `/api/recommendations` +
          `?section=for_you&count=10&availability=${availability}${excludeParam}`,
      );

      if (data && data.length > 0) {
        setMovies(data);
        setIsTmdbFallback(false);
      } else {
        // Aucune reco → fallback TMDB popular
        const page = 1;
        setTmdbPage(page);
        const popular = await loadTmdbPopular(page);
        setMovies(popular);
        setIsTmdbFallback(true);
      }
      setActiveIndex(0);
    } catch {
      // En cas d'erreur API → fallback TMDB
      try {
        const popular = await loadTmdbPopular(1);
        setMovies(popular);
        setIsTmdbFallback(true);
        setTmdbPage(1);
        setActiveIndex(0);
      } catch {
        /* silencieux */
      }
    } finally {
      setLoading(false);
    }
  }, [user, availability, loadTmdbPopular]);

  useEffect(() => {
    seenIds.current.clear();
    setSwipeCount(0);
    loadMovies();
  }, [availability, loadMovies]);

  // ── Swipe ─────────────────────────────────────────────────────
  const handleSwipe = useCallback(
    async (movie: Movie, direction: "Left" | "Right") => {
      const durationMs = Date.now() - swipeStartTime.current;
      swipeStartTime.current = Date.now();

      setLastAction(direction === "Right" ? "like" : "dislike");
      setTimeout(() => setLastAction(null), 600);

      // Marque comme vu
      seenIds.current.add(String(movie.tmdbId));

      // Avance dans la liste
      setActiveIndex((i) => {
        const next = i + 1;
        if (next >= movies.length - 5) {
          if (isTmdbFallback) {
            // Charge la page TMDB suivante
            const nextPage = tmdbPage + 1;
            setTmdbPage(nextPage);
            loadTmdbPopular(nextPage).then((popular) => {
              setMovies((prev) => [...prev.slice(next), ...popular]);
              setActiveIndex(0);
            });
          } else {
            loadMovies();
          }
        }
        return next % movies.length;
      });

      const newCount = swipeCount + 1;
      setSwipeCount(newCount);

      // Appel API swipe
      try {
        await api.post("/api/swipe", {
          movieId: movie.id,
          direction: direction === "Right" ? 1 : 0,
          durationMs,
          context: 0,
        });
      } catch {
        /* silencieux */
      }

      // Mise à jour profil auto
      if (newCount % AUTO_UPDATE_AFTER === 0) {
        try {
          await api.post("/api/recommendations/profile/update");
          setProfileUpdated(true);
          setTimeout(() => setProfileUpdated(false), 2500);
        } catch {
          /* silencieux */
        }
      }
    },
    [movies, swipeCount, loadMovies],
  );

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
    <div className="flex h-screen flex-col overflow-hidden">
      <TopNav />

      {/* ── Filtre disponibilité ── */}
      <div className="border-border flex flex-shrink-0 items-center justify-between border-b px-4 py-3 md:px-12">
        <div className="flex gap-2">
          {AVAILABILITY_OPTIONS.map((opt) => (
            <Button
              key={opt.value}
              variant="secondary"
              size="sm"
              onClick={() => setAvailability(opt.value)}
              className={cn(
                "rounded-pill px-3 py-1 text-xs",
                availability === opt.value &&
                  "border-accent bg-accent/15 text-accent shadow-glow-sm",
              )}
            >
              <i className={opt.icon} aria-hidden="true" /> {opt.label}
            </Button>
          ))}
        </div>

        <div className="flex items-center gap-3">
          {/* Badge fallback TMDB */}
          {isTmdbFallback && (
            <span className="text-text-secondary/60 border-border rounded-pill flex items-center gap-1.5 border px-2.5 py-1 text-xs">
              <i className="ph-thin ph-globe" aria-hidden="true" /> TMDB ·
              Populaires
            </span>
          )}
          {/* Badge profil mis à jour */}
          {profileUpdated && (
            <span className="text-success flex animate-pulse items-center gap-1.5 text-xs">
              <i className="ph-thin ph-check-circle" aria-hidden="true" />{" "}
              Profil mis à jour
            </span>
          )}
          {/* Compteur swipes */}
          <span className="text-text-secondary text-xs">
            {swipeCount} swipe{swipeCount !== 1 ? "s" : ""}
          </span>
        </div>
      </div>

      {/* ── Zone principale ── */}
      <div className="flex min-h-0 flex-1 flex-col">
        {/* Feedback like/dislike */}
        {lastAction && (
          <div
            className={cn(
              "pointer-events-none absolute inset-0 z-20 flex items-center justify-center transition-opacity duration-300",
              lastAction === "like" ? "bg-swipe-like/8" : "bg-swipe-skip/8",
            )}
          >
            <i
              className={cn(
                "text-7xl opacity-80",
                lastAction === "like"
                  ? "ph-thin ph-thumbs-up"
                  : "ph-thin ph-thumbs-down",
              )}
              aria-hidden="true"
            />
          </div>
        )}

        {/* Carrousel */}
        <div className="min-h-0 flex-1">
          {loading ? (
            <div className="flex h-full items-center justify-center">
              <div className="border-accent h-10 w-10 animate-spin rounded-full border-2 border-t-transparent" />
            </div>
          ) : movies.length === 0 ? (
            <div className="text-text-secondary flex h-full flex-col items-center justify-center gap-4">
              <p className="text-sm">Aucun film disponible pour ce filtre.</p>
              <Button variant="outline" onClick={loadMovies}>
                Réessayer
              </Button>
            </div>
          ) : (
            <SwipeFocusRail
              movies={movies}
              activeIndex={activeIndex}
              onSwipe={handleSwipe}
              onIndexChange={setActiveIndex}
            />
          )}
        </div>

        {/* ── Infos film central ── */}

        {/* ── Boutons Like / Dislike ── */}
        <div className="flex justify-center flex-shrink-0 gap-[clamp(6rem,35rem+5vw,80rem)] px-4 pt-3 pb-6 bg-surface">
          <SwipeActionButtons
            swipeState={
              lastAction === "like"
                ? "liked"
                : lastAction === "dislike"
                  ? "disliked"
                  : "idle"
            }
            onSwipe={(direction) =>
              currentMovie && handleSwipe(currentMovie, direction)
            }
            className=" scale-150"
            disabled={!currentMovie || loading}
          />

          
        </div>
                  <h2 className="text-text-primary text-md font-semibold self-auto text-center pb-24">
            Utilise ← / → ou J / L pour décider — clique une carte latérale pour naviguer
          </h2>
      </div>
    </div>
  );
}
