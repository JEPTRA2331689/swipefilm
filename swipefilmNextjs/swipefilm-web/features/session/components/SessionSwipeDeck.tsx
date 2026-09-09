"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { ThumbsUp, ThumbsDown } from "lucide-react";
import { SwipeFocusRail } from "@/components/movie/SwipeFocusRail";
import { SwipeActionButtons } from "@/components/movie/SwipeActionButtons";
import { getSessionPool, sendSessionSwipe } from "@/features/session/api";
import { cn } from "@/lib/utils";
import type { Movie } from "@/types";

// ✅ Recharge le pool un peu avant d'atteindre la fin de la liste plutôt
// qu'au dernier film — évite un flash "aucun film" pendant le fetch.
const RELOAD_MARGIN = 5;

export function SessionSwipeDeck({
  code,
  progressLabel,
}: {
  code: string;
  progressLabel?: string;
}) {
  const [movies, setMovies] = useState<Movie[]>([]);
  const [activeIndex, setActiveIndex] = useState(0);
  const [loading, setLoading] = useState(true);
  const [swipeCount, setSwipeCount] = useState(0);
  const [lastAction, setLastAction] = useState<"like" | "dislike" | null>(null);
  const seenIds = useRef<Set<string>>(new Set());
  const swipeStartTime = useRef<number>(Date.now());
  const loadingMoreRef = useRef(false);

  const currentMovie = movies[activeIndex] ?? null;

  const loadPool = useCallback(async () => {
    setLoading(true);
    try {
      const { pool } = await getSessionPool(code);
      setMovies(pool);
      setActiveIndex(0);
    } finally {
      setLoading(false);
    }
  }, [code]);

  useEffect(() => {
    loadPool();
  }, [loadPool]);

  const handleSwipe = useCallback(
    async (movie: Movie, direction: "Left" | "Right") => {
      const durationMs = Date.now() - swipeStartTime.current;
      swipeStartTime.current = Date.now();

      setLastAction(direction === "Right" ? "like" : "dislike");
      setTimeout(() => setLastAction(null), 600);

      seenIds.current.add(movie.id);

      setActiveIndex((i) => {
        const next = i + 1;
        if (next >= movies.length - RELOAD_MARGIN && !loadingMoreRef.current) {
          loadingMoreRef.current = true;
          getSessionPool(code)
            .then(({ pool }) => {
              const fresh = pool.filter((m) => !seenIds.current.has(m.id));
              setMovies((prev) => [...prev.slice(next), ...fresh]);
              setActiveIndex(0);
            })
            .finally(() => {
              loadingMoreRef.current = false;
            });
        }
        return movies.length > 0 ? next % movies.length : 0;
      });

      setSwipeCount((c) => c + 1);

      try {
        await sendSessionSwipe(code, {
          movieId: movie.contentType === "movie" ? movie.id : undefined,
          seriesId: movie.contentType === "series" ? movie.id : undefined,
          direction,
          durationMs,
        });
      } catch {
        /* silencieux */
      }
    },
    [movies, code],
  );

  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      if (!currentMovie) return;
      if (e.key === "ArrowRight" || e.key === "l" || e.key === "L")
        handleSwipe(currentMovie, "Right");
      if (e.key === "ArrowLeft" || e.key === "j" || e.key === "J")
        handleSwipe(currentMovie, "Left");
    }
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [currentMovie, handleSwipe]);

  return (
    <div className="relative flex min-h-0 flex-1 flex-col">
      <div className="border-border flex flex-shrink-0 items-center justify-between border-b px-4 py-2">
        <span className="text-text-secondary text-xs">
          {swipeCount} swipe{swipeCount !== 1 ? "s" : ""}
        </span>
        {progressLabel && (
          <span className="text-text-secondary text-xs">{progressLabel}</span>
        )}
      </div>

      {lastAction && (
        <div
          className={cn(
            "pointer-events-none absolute inset-0 z-20 flex items-center justify-center transition-opacity duration-300",
            lastAction === "like" ? "bg-swipe-like/8" : "bg-swipe-skip/8",
          )}
        >
          {lastAction === "like" ? (
            <ThumbsUp className="size-16 opacity-80" aria-hidden="true" />
          ) : (
            <ThumbsDown className="size-16 opacity-80" aria-hidden="true" />
          )}
        </div>
      )}

      <div className="min-h-0 flex-1">
        {loading ? (
          <div className="flex h-full items-center justify-center">
            <div className="border-accent h-10 w-10 animate-spin rounded-full border-2 border-t-transparent" />
          </div>
        ) : movies.length === 0 ? (
          <div className="text-text-secondary flex h-full items-center justify-center text-sm">
            Aucun film à swiper pour l&apos;instant.
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

      <div className="gap-fluid-swipe-actions flex flex-shrink-0 justify-center px-4 pt-3 pb-6">
        <SwipeActionButtons
          swipeState={
            lastAction === "like" ? "liked" : lastAction === "dislike" ? "disliked" : "idle"
          }
          onSwipe={(direction) => currentMovie && handleSwipe(currentMovie, direction)}
          disabled={!currentMovie || loading}
        />
      </div>
    </div>
  );
}
