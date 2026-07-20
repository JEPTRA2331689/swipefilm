"use client";

import Image from "next/image";
import Link from "next/link";
import { motion } from "framer-motion";
import { cn, tmdbImage, releaseYear } from "@/lib/utils";
import { AvailabilityDot } from "@/components/ui/Badges";
import type { Movie } from "@/types";

interface MoviePosterProps {
  movie: Movie;
  size?: "sm" | "md" | "original";
  className?: string;
}

export function MoviePoster({ movie, size = "md", className }: MoviePosterProps) {
  const poster = tmdbImage(
    movie.posterPath,
    size === "sm" ? "w185" : size === "original" ? "original" : "original"
  );
  const year = releaseYear(movie.releaseDate);

  const dims =
    size === "sm"
      ? "w-[120px]"
      : size === "md"
      ? "w-[152px]"
      : "w-full flex-1 max-w-full";

  // Échelles typographiques selon la taille
  const typography = {
    rating:
      size === "sm"
        ? "text-xs font-bold"
        : size === "md"
        ? "text-sm font-bold"
        : "text-2xl font-bold",
    title:
      size === "sm"
        ? "text-[10px] leading-tight"
        : size === "md"
        ? "text-xs leading-tight"
        : "text-xl leading-tight",
    meta:
      size === "sm"
        ? "text-[9px] px-1.5 py-0.5"
        : size === "md"
        ? "text-[10px] px-2 py-1"
        : "text-sm px-3 py-1.5",
    dot:
      size === "sm" ? "scale-75" : size === "md" ? "scale-90" : "scale-100",
    padding:
      size === "sm" ? "p-1" : size === "md" ? "p-1.5" : "p-2.5",
    gap:
      size === "sm" ? "gap-1" : size === "md" ? "gap-2" : "gap-3",
  };

  return (
    <motion.div
      whileHover={{ y: -4 }}
      transition={{ duration: 0.18 }}
      className={cn("flex-shrink-0", dims, className)}
    >
      <Link href={`/movie/${movie.id}`} className="block group">
        <div className="relative aspect-[2/3] overflow-hidden bg-surface-alt rounded-card">
          {poster ? (
            <Image
              src={poster}
              alt={movie.title}
              fill
              sizes={
                size === "sm"
                  ? "120px"
                  : size === "original"
                  ? "665px"
                  : "152px"
              }
              className="object-cover transition-transform duration-300 group-hover:scale-105"
            />
          ) : (
            <div className="flex h-full items-center justify-center p-2 text-center text-text-secondary text-xs">
              {movie.title}
            </div>
          )}

          {/* Gradient overlay pour la lisibilité */}
          <div className="absolute inset-0 bg-gradient-to-t from-black/70 via-transparent to-black/20" />

          <div
            className={cn(
              "absolute inset-0 flex flex-col justify-between",
              typography.padding
            )}
          >
            {/* Haut : dot + rating */}
            <div className="flex flex-row items-start justify-between">
              <div className={cn("origin-top-left", typography.dot)}>
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
                  typography.rating
                )}
              >
                {movie.tmdbRating}
              </span>
            </div>

            {/* Bas : titre + méta */}
            <div className={cn("flex flex-col", typography.gap)}>
              <p
                className={cn(
                  "line-clamp-2 font-medium text-text-primary drop-shadow",
                  typography.title
                )}
              >
                {movie.title}
              </p>
              {size !== "sm" && (
                <div className="flex flex-row gap-1.5">
                  <span
                    className={cn(
                      "font-medium text-text-primary bg-black/40 rounded-full backdrop-blur-sm",
                      typography.meta
                    )}
                  >
                    {year}
                  </span>
                  {movie.runtimeMinutes && (
                    <span
                      className={cn(
                        "font-medium text-text-primary bg-black/40 rounded-full backdrop-blur-sm",
                        typography.meta
                      )}
                    >
                      {movie.runtimeMinutes} min
                    </span>
                  )}
                </div>
              )}
            </div>
          </div>
        </div>
      </Link>
    </motion.div>
  );
}