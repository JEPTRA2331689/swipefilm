"use client";

import { useEffect, useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { motion } from "framer-motion";
import { Film, Star } from "lucide-react";
import { getSimilar } from "@/features/catalog/api";
import { AvailabilityDot } from "@/components/ui/Badges";
import { ScrollArrows } from "@/components/ui/ScrollArrows";
import { useHorizontalScroll } from "@/hooks/useHorizontalScroll";
import { cn, tmdbImage } from "@/lib/utils";
import type { Movie } from "@/types";

interface SimilarMediaProps {
  id: string;
  title: string;
  contentType: "movie" | "series";
}

// ✅ Un seul composant pour "Films similaires" et "Séries similaires" —
// même endpoint backend (GetSectionPageAsync isSeries-aware), même carte.
export function SimilarMedia({ id, title, contentType }: SimilarMediaProps) {
  const [items, setItems] = useState<Movie[]>([]);
  const [loading, setLoading] = useState(true);
  const { containerRef, canScrollLeft, canScrollRight, scrollByPage } =
    useHorizontalScroll<HTMLDivElement>();

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    getSimilar({ id, isSeries: contentType === "series", title, count: 12 })
      .then((res) => {
        if (!cancelled) setItems(res);
      })
      .catch(() => {
        if (!cancelled) setItems([]);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [id, title, contentType]);

  if (!loading && items.length === 0) return null;

  return (
    <div className="mt-8 pb-20">
      <div className="mb-5 flex items-baseline justify-between gap-2">
        <div className="flex items-center gap-3">
          <div className="bg-surface border-border flex h-6 w-6 flex-shrink-0 items-center justify-center rounded border">
            <Film className="size-3.5 text-text-secondary" aria-hidden />
          </div>
          <p className="text-fluid-heading-lg text-text-primary font-semibold">
            {contentType === "series" ? "Séries similaires" : "Films similaires"}
          </p>
        </div>
        <ScrollArrows
          canScrollLeft={canScrollLeft}
          canScrollRight={canScrollRight}
          onScrollLeft={() => scrollByPage("left")}
          onScrollRight={() => scrollByPage("right")}
        />
      </div>

      <div className="relative">
        <div className="from-bg-primary pointer-events-none absolute top-0 right-0 bottom-0 z-10 w-12 bg-gradient-to-l to-transparent" />

        <div ref={containerRef} className="scrollbar-hide flex gap-3 overflow-x-auto pb-2">
          {loading
            ? Array.from({ length: 6 }).map((_, i) => (
                <div
                  key={i}
                  className="rounded-card bg-surface-alt aspect-[2/3] w-1/10 flex-shrink-0 animate-pulse"
                />
              ))
            : items.map((m) => <SimilarCard key={`${m.contentType}-${m.id}`} movie={m} />)}
        </div>
      </div>
    </div>
  );
}

function SimilarCard({ movie }: { movie: Movie }) {
  const poster = tmdbImage(movie.posterPath, "original");
  const genre = movie.genres?.[0] ?? null;
  const rating = Math.round(movie.tmdbRating / 2);
  const href = movie.contentType === "series" ? `/series/${movie.id}` : `/movie/${movie.id}`;

  return (
    <Link href={href} className="group w-1/10 flex-shrink-0">
      <motion.div whileHover={{ y: -3 }} transition={{ duration: 0.15 }}>
        <div className="rounded-card bg-surface border-border group-hover:border-accent/50 relative aspect-[2/3] overflow-hidden border shadow-md transition-colors">
          {poster ? (
            <Image
              src={poster}
              alt={movie.title}
              fill
              sizes="fill"
              className="object-cover transition-transform duration-300 group-hover:scale-105"
            />
          ) : (
            <div className="text-text-secondary flex h-full items-center justify-center p-2 text-center text-xs">
              {movie.title}
            </div>
          )}
          {/* Dot dispo */}
          <div className="absolute top-2 left-2">
            <AvailabilityDot
              isAvailable={movie.isAvailable}
              className="ring-bg-primary/50 h-2 w-2"
            />
          </div>
        </div>

        <div className="mt-2 px-0.5">
          <p className="text-text-primary group-hover:text-accent line-clamp-2 text-[12px] leading-tight font-bold tracking-wide uppercase transition-colors">
            {movie.title}
          </p>
          {genre && (
            <p className="text-text-secondary mt-0.5 text-[10px] tracking-widest uppercase">
              {genre}
            </p>
          )}
          <div className="mt-1 flex gap-0.5">
            {Array.from({ length: 5 }).map((_, i) => (
              <Star
                key={i}
                className={cn(
                  "size-2.5",
                  i < rating ? "text-warning" : "text-border",
                )}
                aria-hidden
              />
            ))}
          </div>
        </div>
      </motion.div>
    </Link>
  );
}
