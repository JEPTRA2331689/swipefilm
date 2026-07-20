"use client";

import { useEffect, useState } from "react";
import { motion } from "framer-motion";
import type { Movie } from "@/types";
import { MoviePoster } from "@/components/movie/MoviePoster";
import { useContainerSize } from "@/hooks/useContainerSize";

// Scale multiplier per step away from center
const STEP = 0.6;

// Number of posters visible on each side of center
const SIDE = 4;

function getScale(distance: number): number {
  return Math.pow(STEP, distance);
}

function getVisualW(distance: number, baseW: number): number {
  return baseW * getScale(distance);
}

function getCenterX(n: number, gap: number, baseW: number): number {
  if (n === 0) return 0;

  const sign = Math.sign(n);
  let total = 0;

  for (let d = 0; d < Math.abs(n); d++) {
    total += getVisualW(d, baseW) / 2 + gap + getVisualW(d + 1, baseW) / 2;
  }

  return sign * total;
}

export interface VerticalPosterCarouselProps {
  movies: Movie[];
  /** "auto" scrolls by itself; "swipe" waits for a horizontal drag gesture */
  mode: "auto" | "swipe";
  autoScrollInterval?: number;
}

export function VerticalPosterCarousel({
  movies,
  mode,
  autoScrollInterval = 300000,
}: VerticalPosterCarouselProps) {
  const [active, setActive] = useState(0);
  const { ref, size } = useContainerSize();
  const count = movies.length;

  useEffect(() => {
    if (mode !== "auto" || count === 0) return;
    const id = setInterval(
      () => setActive((i) => (i + 1) % count),
      autoScrollInterval,
    );
    return () => clearInterval(id);
  }, [mode, count, autoScrollInterval]);

  if (count === 0 || size.height === 0) {
    return <div ref={ref} className="relative h-full w-full" />;
  }

  // ── Base poster size derived from screen size ──
  // Center poster height is a fraction of the container height,
  // clamped so it never overflows on very small or very large screens.
  const BASE_H = size.height * 0.78;

  const BASE_W = BASE_H * (2 / 3); // keep 2:3 poster ratio

  const gap = size.width < 480 ? 10 : size.width < 768 ? 16 : 24;

  return (
    <motion.div
      ref={ref}
      className="relative h-full w-full touch-none overflow-hidden select-none"
      drag={mode === "swipe" ? "x" : false}
      dragConstraints={{ left: 0, right: 0 }}
      dragElastic={0.15}
      onDragEnd={(_, { offset }) => {
        if (Math.abs(offset.x) < 50) return;
        setActive((i) =>
          offset.x < 0 ? (i + 1) % count : (i - 1 + count) % count,
        );
      }}
    >
      {Array.from({ length: SIDE * 2 + 1 }, (_, i) => i - SIDE).map(
        (offset) => {
          const idx = (((active + offset) % count) + count) % count;
          const dist = Math.abs(offset);
          const scale = getScale(dist);

          return (
            <motion.div
              key={offset}
              className="pointer-events-none absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2"
              animate={{
                x: getCenterX(offset, gap, BASE_W),
                scale,
                opacity: Math.pow(0.7, dist),
              }}
              transition={{
                type: "spring",
                stiffness: 110,
                damping: 24,
                mass: 1.3,
                restDelta: 0.001,
                duration: 0.6,
                ease: [0.22, 1, 0.36, 1],
              }}
            >
              <MoviePoster movie={movies[idx]} size="original" />
            </motion.div>
          );
        },
      )}
    </motion.div>
  );
}
