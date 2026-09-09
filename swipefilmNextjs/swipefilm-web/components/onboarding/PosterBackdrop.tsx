"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import type { Movie } from "@/types";
import { PosterSlider } from "./PosterSlider";

export function PosterBackdrop() {
  const [posters, setPosters] = useState<Movie[]>([]);

  useEffect(() => {
    api
      .get<{ posters: Movie[] }>("/api/tmdb/popular", { skipAuth: true })
      .then((data) => setPosters(data.posters))
      .catch(() => {});
  }, []);

  return (
    <div className="absolute inset-0 overflow-hidden" aria-hidden="true">
      <PosterSlider movies={posters} />

      <div className="bg-bg-primary/30 absolute inset-0" />
    </div>
  );
}
