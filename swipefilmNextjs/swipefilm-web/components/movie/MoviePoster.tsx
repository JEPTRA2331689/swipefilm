"use client";

import Image from "next/image";
import Link from "next/link";
import { motion } from "framer-motion";
import { cn, tmdbImage, releaseYear } from "@/lib/utils";
import { AvailabilityDot } from "@/components/ui/Badges";
import type { Movie } from "@/types";

interface MoviePosterProps {
  movie: Movie;
  size?: "sm" | "md" | "lg" | "original";
  className?: string;
  priority?: boolean;
}

export function MoviePoster({
  movie,
  size = "md",
  className,
  priority = false,
}: MoviePosterProps) {
  const poster = tmdbImage(
    movie.posterPath,
    size === "sm"
      ? "w185"
      : size === "md"
        ? "w342"
        : size === "lg"
          ? "w500"
          : "w780",
  );
  const year = releaseYear(movie.releaseDate);
  const href =
    movie.contentType === "series"
      ? `/series/${movie.id}`
      : `/movie/${movie.id}`;

  const dims =
    size === "sm"
      ? "w-[100px]"
      : size === "md"
        ? "w-[152px]"
        : size === "lg"
          ? "w-[200px]"
          : "w-full flex-1 max-w-full";

  return (
    <motion.div
      whileHover={{ y: -4 }}
      transition={{ duration: 0.18 }}
      className={cn("flex-shrink-0", dims, className)}
    >
      <Link href={href} className="group block">
        <div className="bg-surface-alt @container relative aspect-[2/3] overflow-hidden">
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
              priority={priority}
            />
          ) : (
            <div className="text-text-secondary flex h-full items-center justify-center p-2 text-center text-xs">
              {movie.title}
            </div>
          )}

          {/* Badge de disponibilité — seul élément visible en tout temps */}
          <div className="absolute top-2 left-2 z-10">
            <AvailabilityDot isAvailable={movie.isAvailable} />
          </div>

          {/* Dégradé + texte — révélés uniquement au survol */}
          <div className="absolute inset-0 bg-gradient-to-t from-black/80 via-black/10 to-black/30 opacity-0 transition-opacity duration-200 group-hover:opacity-100" />

          <div className="absolute inset-0 flex flex-col justify-between p-2 opacity-0 transition-opacity duration-200 group-hover:opacity-100">
            {/* Haut : rating */}
            <div className="flex justify-end">
              <span className="font-display text-fluid-poster-score text-accent-light drop-shadow">
                {movie.tmdbRating} *
              </span>
            </div>

            {/* Bas : titre + méta */}
            <div className="flex flex-col gap-1.5">
              <p className="text-fluid-poster-title text-text-primary-inside-poster line-clamp-2 font-medium drop-shadow">
                {movie.title}
              </p>
              {size !== "sm" && (
                <div className="flex flex-row gap-1.5">
                  <span className="text-fluid-poster-meta text-text-primary-inside-poster rounded-full bg-black/40 px-2 py-1 font-medium backdrop-blur-sm">
                    {year}
                  </span>
                  {movie.runtimeMinutes && (
                    <span className="text-fluid-poster-meta text-text-primary-inside-poster rounded-full bg-black/40 px-2 py-1 font-medium backdrop-blur-sm">
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
