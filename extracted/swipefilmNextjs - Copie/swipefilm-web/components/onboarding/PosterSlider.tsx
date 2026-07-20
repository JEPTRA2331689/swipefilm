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

export function PosterSlider({ movies, autoplayDelay = 3000 }: PosterSliderProps) {
    const loopedMovies = [
  ...movies.map(m => ({ ...m, _key: `a-${m.id}` })),
  ...movies.map(m => ({ ...m, _key: `b-${m.id}` })),
  ...movies.map(m => ({ ...m, _key: `c-${m.id}` })),
];
  const startIndex = Math.floor(loopedMovies.length / 2);
  const [emblaRef, emblaApi] = useEmblaCarousel(
    { loop: true, align: "center", startIndex,   skipSnaps: true,},
    [AutoScroll({   speed: 3,
  stopOnInteraction: false, })]
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
    <div className="w-full h-full relative">
      {/* Carousel */}
      <div className="overflow-hidden w-full h-full " ref={emblaRef}>
        <div className="flex items-center w-full h-full ">
          {loopedMovies.map((movie, index) => {
            const isSelected = selectedIndex === index;
            return (
              /* Correction de "h-10/12 w" par des classes flex-shrink-0 et dimensions explicites */
              <div
                key={movie._key}
                className={`relative overflow-hidden flex-none h-5/6 transition-all duration-300 aspect-[2/3] bg-amber-500 ${
                  isSelected     
                    ? "scale-100 opacity-100 z-10"
                    : "scale-90 opacity-40 z-0"
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
      <div className="absolute bottom-4 left-0 right-0 flex justify-center gap-2 z-30">
        {loopedMovies.map((_, index) => (
          <button
            key={index}
            onClick={() => emblaApi?.scrollTo(index)}
            className={`rounded-full transition-all duration-100 ${
              selectedIndex === index
                ? "w-5 h-2.5 bg-accent"
                : "w-2.5 h-2.5 bg-text-secondary/30 hover:bg-text-secondary/60"
            }`}
          />
        ))}
      </div>
    </div>
  );
}
