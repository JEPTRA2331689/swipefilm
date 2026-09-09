"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import type { Movie } from "@/types";

const TMDB_IMG = "https://image.tmdb.org/t/p/original";
const ROTATION_SPEED = 6000;

// ✅ Repris du pattern Overseerr (src/components/Common/ImageFader, MIT) —
// des backdrops en fondu enchaîné plutôt que le carrousel 3D utilisé sur
// /login (PosterBackdrop, laissé tel quel là-bas).
export function SetupBackdrop() {
  const [backdrops, setBackdrops] = useState<string[]>([]);
  const [activeIndex, setActiveIndex] = useState(0);

  useEffect(() => {
    api
      .get<{ posters: Movie[] }>("/api/tmdb/popular", { skipAuth: true })
      .then((data) => {
        const paths = data.posters
          .map((m) => m.backdropPath)
          .filter((p): p is string => !!p);
        setBackdrops(paths);
      })
      .catch(() => {});
  }, []);

  useEffect(() => {
    if (backdrops.length === 0) return;
    const interval = setInterval(
      () => setActiveIndex((i) => (i + 1) % backdrops.length),
      ROTATION_SPEED,
    );
    return () => clearInterval(interval);
  }, [backdrops]);

  return (
    <div className="absolute inset-0 overflow-hidden" aria-hidden="true">
      {backdrops.map((path, i) => (
        <div
          key={path}
          className={`absolute inset-0 bg-cover bg-center transition-opacity duration-1000 ease-in ${
            i === activeIndex ? "opacity-100" : "opacity-0"
          }`}
          style={{ backgroundImage: `url(${TMDB_IMG}${path})` }}
        />
      ))}
      <div className="from-bg-primary/50 to-bg-primary absolute inset-0 bg-gradient-to-b" />
    </div>
  );
}
