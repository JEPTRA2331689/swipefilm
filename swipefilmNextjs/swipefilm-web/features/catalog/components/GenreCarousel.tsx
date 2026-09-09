"use client";

import { useEffect, useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { getGenreCards, type GenreCard } from "@/features/catalog/api";
import { ScrollArrows } from "@/components/ui/ScrollArrows";
import { useHorizontalScroll } from "@/hooks/useHorizontalScroll";
import { tmdbImage } from "@/lib/utils";
import type { AvailabilityFilter } from "@/types";

// ✅ Façon Overseerr : pas d'image dédiée par genre, on affiche le backdrop
// d'un titre représentatif (déjà résolu côté backend) sous un dégradé + le
// nom du genre. Clic → page d'exploration filtrée par genre.
export function GenreCarousel({
  availability = 0,
}: {
  availability?: AvailabilityFilter;
}) {
  const [genres, setGenres] = useState<GenreCard[]>([]);
  const [loading, setLoading] = useState(true);
  const { containerRef, canScrollLeft, canScrollRight, scrollByPage } =
    useHorizontalScroll<HTMLDivElement>();

  useEffect(() => {
    getGenreCards()
      .then(setGenres)
      .catch(() => setGenres([]))
      .finally(() => setLoading(false));
  }, []);

  if (!loading && genres.length === 0) return null;

  return (
    <section className="border-border border-t pt-5 pb-2 sm:pt-7">
      <div className="mb-3 flex items-baseline justify-between gap-2 px-3 sm:gap-3 sm:px-6 md:px-12">
        <div className="flex items-center gap-3">
          <div className="bg-accent h-5 w-0.5 rounded-full opacity-70" />
          <h2 className="font-display text-text-primary text-base sm:text-[17px] lg:text-[19px]">
            Parcourir par genre
          </h2>
        </div>
        <ScrollArrows
          canScrollLeft={canScrollLeft}
          canScrollRight={canScrollRight}
          onScrollLeft={() => scrollByPage("left")}
          onScrollRight={() => scrollByPage("right")}
        />
      </div>

      <div className="relative">
        <div className="from-bg-primary pointer-events-none absolute top-0 bottom-0 left-0 z-10 w-8 bg-gradient-to-r to-transparent sm:w-12 md:w-16" />
        <div className="from-bg-primary pointer-events-none absolute top-0 right-0 bottom-0 z-10 w-8 bg-gradient-to-l to-transparent sm:w-12 md:w-16" />

        <div
          ref={containerRef}
          className="scrollbar-hide flex snap-x snap-proximity gap-2.5 overflow-x-auto scroll-smooth px-3 pb-3 sm:gap-3 sm:px-6 md:gap-3.5 md:px-12"
        >
          {loading
            ? Array.from({ length: 6 }).map((_, i) => (
                <div
                  key={i}
                  className="bg-surface-alt aspect-video w-[400px] flex-shrink-0 animate-pulse rounded-lg"
                />
              ))
            : genres.map((g) => {
                const backdrop = tmdbImage(g.backdropPath, "w500");
                return (
                  <Link
                    key={g.genre}
                    href={`/browse?genre=${encodeURIComponent(g.genre)}&title=${encodeURIComponent(g.genre)}&availability=${availability}`}
                    className="group relative aspect-video w-[400px] flex-shrink-0 snap-start overflow-hidden rounded-lg"
                  >
                    <div className="bg-surface-alt absolute inset-0">
                      {backdrop && (
                        <Image
                          src={backdrop}
                          alt=""
                          fill
                          sizes="280px"
                          className="object-cover transition-transform duration-300 group-hover:scale-105"
                          aria-hidden
                        />
                      )}
                    </div>
                    <div className="absolute inset-0 bg-black/50 transition-colors group-hover:bg-black/40" />
                    <div className="absolute inset-0 flex items-center justify-center p-2">
                      <span className="font-display text-text-primary-inside-poster text-center text-sm font-semibold drop-shadow sm:text-3xl">
                        {g.genre}
                      </span>
                    </div>
                  </Link>
                );
              })}
        </div>
      </div>
    </section>
  );
}
