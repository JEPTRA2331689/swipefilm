"use client";

import * as React from "react";
import { motion, AnimatePresence, type PanInfo } from "framer-motion";
import { Clapperboard } from "lucide-react";
import type { Movie } from "@/types";
import { MoviePoster } from "./MoviePoster";

const TMDB_IMG = "https://image.tmdb.org/t/p";

function wrap(min: number, max: number, v: number) {
  const rangeSize = max - min;
  return ((((v - min) % rangeSize) + rangeSize) % rangeSize) + min;
}

const BASE_SPRING = {
  type: "spring",
  stiffness: 300,
  damping: 30,
  mass: 1,
} as const;
const TAP_SPRING = {
  type: "spring",
  stiffness: 450,
  damping: 18,
  mass: 1,
} as const;

function posterUrl(movie: Movie): string {
  if (!movie.posterPath) return "/placeholder-poster.jpg";
  if (movie.posterPath.startsWith("http")) return movie.posterPath;
  return `${TMDB_IMG}/w500${movie.posterPath}`;
}

export interface SwipeFocusRailProps {
  movies: Movie[];
  activeIndex: number;
  onSwipe: (movie: Movie, direction: "Left" | "Right") => void;
  onIndexChange: (index: number) => void;
}

const VISIBLE = [-2, -1, 0, 1, 2];

export function SwipeFocusRail({
  movies,
  activeIndex,
  onSwipe,
  onIndexChange,
}: SwipeFocusRailProps) {
  const count = movies.length;

  const activeMovie = movies[wrap(0, count, activeIndex)];

  function go(delta: -1 | 1) {
    const next = wrap(0, count, activeIndex + delta);
    onIndexChange(next);
  }

  function handleDragEnd(
    _: MouseEvent | TouchEvent | PointerEvent,
    { offset, velocity }: PanInfo,
  ) {
    if (offset.x < -150 || velocity.x < -700) {
      onSwipe(activeMovie, "Left");
      return;
    }
    if (offset.x > 150 || velocity.x > 700) {
      onSwipe(activeMovie, "Right");
      return;
    }
    if (offset.x < -60) go(1);
    else if (offset.x > 60) go(-1);
  }

  function handleKeyDown(e: React.KeyboardEvent) {
    if (e.key === "ArrowLeft") {
      e.preventDefault();
      go(-1);
    }
    if (e.key === "ArrowRight") {
      e.preventDefault();
      go(1);
    }
  }

  if (count === 0) return null;

  return (
    <div
      className="relative h-full w-full overflow-hidden outline-none select-none"
      onKeyDown={handleKeyDown}
      tabIndex={-1}
    >
      {/* Ambience backdrop */}
      <div className="pointer-events-none absolute inset-0 z-0">
        <AnimatePresence mode="popLayout">
          <motion.div
            key={`bg-${activeMovie?.id}`}
            initial={{ opacity: 0 }}
            animate={{ opacity: 0.35 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.9, ease: "easeOut" }}
            className="absolute inset-0"
          >
            {activeMovie && (
              <img
                src={posterUrl(activeMovie)}
                alt=""
                className="h-full w-full scale-110 object-cover blur-3xl saturate-150"
              />
            )}
            <div className="from-bg-primary via-bg-primary/60 absolute inset-0 bg-gradient-to-t to-transparent" />
          </motion.div>
        </AnimatePresence>
      </div>

      {/* 3D Rail */}
      <motion.div
        className="relative z-10 flex h-full w-full items-center justify-center"
        style={{ perspective: 1200 }}
      >
        {VISIBLE.map((offset) => {
          const idx = wrap(0, count, activeIndex + offset);
          const movie = movies[idx];
          const isCenter = offset === 0;
          const dist = Math.abs(offset);

          return (
            <motion.div
              key={movie.id}
              className="absolute"
              style={{ transformStyle: "preserve-3d" }}
              initial={false}
              animate={{
                x: offset * 260,
                z: -dist * 160,
                scale: isCenter ? 1 : 0.82,
                rotateY: offset * -18,
                opacity: isCenter ? 1 : Math.max(0.08, 0.6 - dist * 0.45),
                filter: `blur(${isCenter ? 0 : dist * 5}px) brightness(${isCenter ? 1 : 0.45})`,
              }}
              transition={{ scale: TAP_SPRING, default: BASE_SPRING }}
              drag={isCenter ? "x" : false}
              dragConstraints={{ left: 0, right: 0 }}
              dragElastic={0.18}
              onDragEnd={isCenter ? handleDragEnd : undefined}
              whileDrag={{ scale: 1.05, rotate: 5, cursor: "grabbing" }}
            >
              <div className="bg-surface aspect-[2/3] size-fluid-swipe-poster overflow-hidden border-t border-white/15 shadow-2xl">
                {movie.posterPath ? (
                  <MoviePoster
                    movie={movies[idx]}
                    size="original"
                    className="pointer-events-none"
                  />
                ) : (
                  <div className="bg-surface-alt flex h-full w-full items-center justify-center">
                    <Clapperboard className="size-8 text-text-secondary/30" />
                  </div>
                )}
                <div className="pointer-events-none absolute inset-0 rounded-2xl bg-gradient-to-b from-white/8 to-transparent" />
              </div>
            </motion.div>
          );
        })}
      </motion.div>
    </div>
  );
}
