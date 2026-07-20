"use client";

import Image from "next/image";
import Link from "next/link";
import { motion } from "framer-motion";
import { cn, tmdbImage, releaseYear } from "@/lib/utils";
import type { Movie } from "@/types";

interface MovieCardProps {
  movie: Movie;
  size?: "sm" | "md";
  className?: string;
}

export function MovieCard({ movie, size = "md", className }: MovieCardProps) {
  const poster = tmdbImage(movie.posterPath, size === "sm" ? "w185" : "w300");
  const year = releaseYear(movie.releaseDate);
  const dims = size === "sm" ? "w-[120px]" : "w-[152px]";
  const href =
    movie.contentType === "series"
      ? `/series/${movie.id}`
      : `/movie/${movie.id}`;

  return (
    <motion.div
      whileHover={{ y: -5, scale: 1.02 }}
      transition={{ duration: 0.18, ease: "easeOut" }}
      className={cn("flex-shrink-0", dims, className)}
    >
      <Link href={href} className="group block">
        <div className="rounded-card bg-surface-alt border-border group-hover:border-accent/50 relative aspect-[2/3] overflow-hidden border shadow-md transition-colors duration-200 group-hover:shadow-[0_8px_24px_rgba(192,132,252,0.2)]">
          {poster ? (
            <Image
              src={poster}
              alt={movie.title}
              fill
              sizes="(max-width: 768px) 120px, 152px"
              className="object-cover transition-transform duration-300 group-hover:scale-105"
            />
          ) : (
            <div className="text-text-secondary flex h-full items-center justify-center p-2 text-center text-xs">
              {movie.title}
            </div>
          )}

          {/* Gradient bottom pour lisibilité */}
          <div className="from-bg-primary/80 absolute inset-x-0 bottom-0 h-16 bg-gradient-to-t to-transparent" />

          {/* Badge disponibilité */}
          <div className="absolute top-2 left-2">
            <div
              className={cn(
                "ring-bg-primary/60 h-2.5 w-2.5 rounded-full ring-2",
                movie.isAvailable ? "bg-success" : "bg-swipe-star",
              )}
              title={movie.isAvailable ? "Disponible" : "Non disponible"}
            />
          </div>

          {/* Note TMDB */}
          <div className="bg-bg-primary/75 absolute right-2 bottom-2 flex items-center gap-1 rounded-md px-1.5 py-0.5 backdrop-blur-sm">
            <span className="text-warning text-[9px]">★</span>
            <span className="text-text-primary text-[10px] font-bold">
              {movie.tmdbRating.toFixed(1)}
            </span>
          </div>
        </div>

        <div className="mt-2 px-0.5">
          <p className="text-text-primary group-hover:text-accent line-clamp-2 text-[13px] leading-tight font-medium transition-colors duration-150">
            {movie.title}
          </p>
          <p className="text-text-secondary mt-0.5 text-[11px]">{year}</p>
        </div>
      </Link>
    </motion.div>
  );
}
