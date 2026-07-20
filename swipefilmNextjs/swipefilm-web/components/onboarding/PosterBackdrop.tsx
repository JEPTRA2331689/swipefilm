"use client";

import { useEffect, useState } from "react";
import type { Movie } from "@/types";
import { PosterSlider } from "./PosterSlider";

export function PosterBackdrop() {
  const [posters, setPosters] = useState<Movie[]>([]);

  useEffect(() => {
    fetch("/api/tmdb/popular")
      .then((res) => res.json())
      .then((data) => {
        if (Array.isArray(data.posters)) setPosters(data.posters);
      })
      .catch(() => {});
  }, []);

  console.log(posters);

  return (
    <div className="absolute inset-0 overflow-hidden" aria-hidden="true">
      <PosterSlider movies={posters} />

      <div className="bg-bg-primary/30 absolute inset-0" />
    </div>
  );
}
