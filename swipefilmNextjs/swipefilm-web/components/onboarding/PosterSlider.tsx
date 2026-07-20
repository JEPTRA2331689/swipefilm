"use client";

import React, { useEffect, useCallback, useState } from "react";
import useEmblaCarousel from "embla-carousel-react";
import Autoplay from "embla-carousel-autoplay";
import type { Movie } from "@/types";
import { MoviePoster } from "../movie/MoviePoster";
import AutoScroll from "embla-carousel-auto-scroll";

interface PosterSliderProps {
  movies: Movie[];
  autoplayDelay?: number;
}

export function PosterSlider({
  movies,
  autoplayDelay = 3000,
}: PosterSliderProps) {
  const loopedMovies = [
    ...movies.map((m) => ({ ...m, _key: `a-${m.id}` })),
    ...movies.map((m) => ({ ...m, _key: `b-${m.id}` })),
    ...movies.map((m) => ({ ...m, _key: `c-${m.id}` })),
  ];
  const startIndex = Math.floor(loopedMovies.length / 2);
  const [emblaRef, emblaApi] = useEmblaCarousel(
    { loop: true, align: "center", startIndex, skipSnaps: true },
    [AutoScroll({ speed: 3, stopOnInteraction: false })],
  );
  const [selectedIndex, setSelectedIndex] = useState(0);

  const onSelect = useCallback(() => {
    if (!emblaApi) return;
    setSelectedIndex(emblaApi.selectedScrollSnap());
  }, [emblaApi]);

  useEffect(() => {
    if (!emblaApi) return;

    onSelect();

    emblaApi.on("select", onSelect);
    emblaApi.on("reInit", onSelect);

    return () => {
      emblaApi.off("select", onSelect);
      emblaApi.off("reInit", onSelect);
    };
  }, [emblaApi, onSelect]);
  // Tripler le tableau pour garantir un loop vraiment invisible

  if (!movies.length) return null;
  return (
    <div className="relative h-full w-full">
      {/* Carousel */}
      <div className="h-full w-full overflow-hidden" ref={emblaRef}>
        <div className="flex h-full w-full items-center">
          {loopedMovies.map((movie, index) => {
            const isSelected = selectedIndex === index;
            return (
              /* Correction de "h-10/12 w" par des classes flex-shrink-0 et dimensions explicites */
              <div
                key={movie._key}
                className={`relative aspect-[2/3] h-5/6 flex-none overflow-hidden bg-amber-500 transition-all duration-300 ${
                  isSelected
                    ? "z-10 scale-100 opacity-100"
                    : "z-0 scale-90 opacity-40"
                }`}
              >
                {/* Grâce à React.memo, ce composant ne clignote plus et ne re-render plus au défilement */}
                <MoviePoster movie={movie} size="original" />
              </div>
            );
          })}
        </div>
      </div>

      {/* Dots */}
      <div className="absolute right-0 bottom-4 left-0 z-30 flex justify-center gap-2">
        {loopedMovies.map((_, index) => (
          <button
            key={index}
            onClick={() => emblaApi?.scrollTo(index)}
            className={`rounded-full transition-all duration-100 ${
              selectedIndex === index
                ? "bg-accent h-2.5 w-5"
                : "bg-text-secondary/30 hover:bg-text-secondary/60 h-2.5 w-2.5"
            }`}
          />
        ))}
      </div>
    </div>
  );
}
