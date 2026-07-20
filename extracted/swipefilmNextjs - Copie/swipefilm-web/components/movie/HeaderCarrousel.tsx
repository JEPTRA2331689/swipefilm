"use client";

import React, { useCallback, useEffect, useRef, useState } from "react";
import useEmblaCarousel from "embla-carousel-react";
import Autoplay from "embla-carousel-autoplay";
import Image from "next/image";
import Link from "next/link";
import type { Movie } from "@/types";
import { releaseYear, cn } from "@/lib/utils";

interface AutoCarouselProps {
  movies: Movie[];
  delay?: number;
  className?: string;
}

export function AutoCarousel({
  movies,
  delay = 4000,
  className,
}: AutoCarouselProps) {
  const [selectedIndex, setSelectedIndex] = useState(0);
  const [progress, setProgress] = useState(0);
  const [backdrops, setBackdrops] = useState<Record<number, string | null>>({});
  const progressRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const progressStart = useRef<number>(Date.now());

  const autoplay = useRef(
    Autoplay({ delay, stopOnInteraction: false, stopOnMouseEnter: true })
  );

  const [emblaRef, emblaApi] = useEmblaCarousel(
    { loop: true, align: "center" },
    [autoplay.current]
  );

  // ── Fetch backdrops depuis TMDB pour tous les films ────────────
  useEffect(() => {
    if (!movies.length) return;

    Promise.all(
      movies.map(async (movie) => {
        try {
          const res = await fetch(`/api/tmdb/movie/${movie.tmdbId}`);
          const data = await res.json();
          return [movie.tmdbId, data.backdropPath ?? null] as [number, string | null];
        } catch {
          return [movie.tmdbId, null] as [number, string | null];
        }
      })
    ).then((entries) => {
      setBackdrops(Object.fromEntries(entries));
    });
  }, [movies]);

  // ── Sélection ──────────────────────────────────────────────────
  const onSelect = useCallback(() => {
    if (!emblaApi) return;
    setSelectedIndex(emblaApi.selectedScrollSnap());
    setProgress(0);
    progressStart.current = Date.now();
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

  // ── Jauge de progression ────────────────────────────────────────
  useEffect(() => {
    if (progressRef.current) clearInterval(progressRef.current);
    progressStart.current = Date.now();
    const tick = 16;
    progressRef.current = setInterval(() => {
      const elapsed = Date.now() - progressStart.current;
      setProgress(Math.min((elapsed / delay) * 100, 100));
    }, tick);
    return () => { if (progressRef.current) clearInterval(progressRef.current); };
  }, [selectedIndex, delay]);

  const handleMouseEnter = useCallback(() => {
    if (progressRef.current) clearInterval(progressRef.current);
  }, []);

  const handleMouseLeave = useCallback(() => {
    progressStart.current = Date.now() - (progress / 100) * delay;
    const tick = 16;
    progressRef.current = setInterval(() => {
      const elapsed = Date.now() - progressStart.current;
      setProgress(Math.min((elapsed / delay) * 100, 100));
    }, tick);
  }, [progress, delay]);

  if (!movies.length) return null;

  const current = movies[selectedIndex];

  return (
    <div
      className={cn("relative w-full select-none", className)}
      onMouseEnter={handleMouseEnter}
      onMouseLeave={handleMouseLeave}
    >
      {/* ── Embla viewport ── */}
      <div className="overflow-hidden rounded-card" ref={emblaRef}>
        <div className="flex">
          {movies.map((movie, i) => {
            const backdrop = backdrops[movie.tmdbId];
            const isSelected = selectedIndex === i;

            return (
              <div
                key={movie.id}
                className="flex-[0_0_90%] sm:flex-[0_0_70%] md:flex-[0_0_60%] px-2"
              >
                <Link href={`/movie/${movie.id}`}>
                  <div
                    className={cn(
                      "relative aspect-video overflow-hidden rounded-card border transition-all duration-300 h-fill",
                      isSelected
                        ? "border-accent shadow-[0_0_28px_rgba(192,132,252,0.3)] scale-100 opacity-100"
                        : "border-border scale-95 opacity-40 hover:opacity-60"
                    )}
                  >
                    {backdrop ? (
                      <Image
                        src={backdrop}
                        alt={movie.title}
                        fill
                        sizes="(max-width: 640px) 90vw, (max-width: 768px) 70vw, 60vw"
                        className="object-cover transition-transform duration-500 hover:scale-105"
                      />
                    ) : (
                      // Fallback poster si pas de backdrop encore chargé
                      <div className="flex h-full items-center justify-center bg-surface-alt text-text-secondary text-sm p-4 text-center">
                        {movie.title}
                      </div>
                    )}

                    {/* Gradient bas */}
                    <div className="absolute inset-x-0 bottom-0 h-2/5 bg-gradient-to-t from-bg-primary/95 to-transparent" />

                    {/* Infos sur la carte sélectionnée */}
                    {isSelected && (
                      <div className="flex flex-col justify-end absolute h-1/2  bottom-3 left-4 right-4 
                      sm:gap-3">
                        <p className=" font-display md:text-xl lg:text-6xl font-semibold text-text-primary line-clamp-1 drop-shadow">
                          {movie.title}
                        </p>
                          <p className="font-display text-sm text-text-primary drop-shadow md:line-clamp-3 lg:line-clamp-10 lg:w-1/2 ">
                            {movie.overview}
                          </p>

                        <div className="flex items-center gap-2 mt-0.5 text-xs text-text-secondary">

                          {movie.genres?.slice(0, 4).map((g) => (
                            <span
                              key={g}
                              className="text-text-primary bg-black/40 border-amber-50 border-2 rounded-full backdrop-blur-sm 
                              sm:text-[9px] sm:px-1.5 sm:py-0.5,
                              md:text-[10px] md:px-2 md:py-1
                              lg:text-sm lg:px-3 lg:py-1.5"
                            >
                              {g}
                            </span>
                          ))}
                          {releaseYear(movie.releaseDate) && (
                            <span
                            className="font-medium text-text-primary border-amber-50 border-2 rounded-full backdrop-blur-sm 
                              sm:text-[9px] sm:px-1.5 sm:py-0.5,
                              md:text-[10px] md:px-2 md:py-1
                              lg:text-sm lg:px-3 lg:py-1.5">
                                {releaseYear(movie.releaseDate)}
                                </span>
                          )}
                        </div>
                      </div>
                    )}

                    {/* Badge dispo */}
                    <div className="flex flex-row items-start justify-between p-2">
                      <div className={cn("origin-top-left", "sm:scale-100 lg:scale-200")}>
                        <div className="absolute top-2 left-2">
                          <div
                            className={cn(
                              "h-2.5 w-2.5 rounded-full ring-2 ring-bg-primary/60",
                              movie.isAvailable
                                ? "bg-success"
                                : "bg-swipe-star"
                            )}
                            title={movie.isAvailable ? "Disponible" : "Non disponible"}
                          />
                  </div>
                      </div>
                      
                      <span
                        className={cn(
                          "text-text-primary drop-shadow",
                          "text-2xl font-bold"
                        )}
                      >
                        {movie.tmdbRating}
                      </span>
                    </div>
                  </div>
                </Link>
              </div>
            );
          })}
        </div>
      </div>

      {/* ── Dots + jauge ── */}
      <div className="mt-3 flex flex-col items-center gap-2">
        <div className="flex gap-1.5">
          {movies.map((_, i) => (
            <button
              key={i}
              onClick={() => emblaApi?.scrollTo(i)}
              className={cn(
                "rounded-full transition-all duration-200",
                selectedIndex === i
                  ? "w-5 h-2 bg-accent"
                  : "w-2 h-2 bg-text-secondary/30 hover:bg-text-secondary/60"
              )}
              aria-label={`Film ${i + 1}`}
            />
          ))}
        </div>

        {/* Jauge */}
        <div className="w-32 h-0.5 rounded-full bg-border overflow-hidden">
          <div
            className="h-full rounded-full bg-accent transition-none"
            style={{ width: `${progress}%` }}
          />
        </div>
      </div>
    </div>
  );
}