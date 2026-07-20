"use client";

import { MovieCard } from "@/components/movie/MovieCard";
import type { HomeSection } from "@/types";
import { MoviePoster } from "./MoviePoster";

interface SectionRowProps {
  section: HomeSection;
}

export function SectionRow({ section }: SectionRowProps) {
  if (!section.movies.length) return null;

  return (
    <section className="border-border border-t pt-5 pb-2 sm:pt-7">
      <div className="mb-3 flex items-baseline justify-between gap-2 px-3 sm:gap-3 sm:px-6 md:px-12">
        <div className="flex items-center gap-3">
          {/* Accent bar */}
          <div className="bg-accent h-5 w-0.5 rounded-full opacity-70" />
          <h2 className="font-display text-text-primary text-base font-semibold sm:text-[17px] lg:text-[19px]">
            {section.title}
          </h2>
        </div>
        <span className="text-text-secondary text-[11px] whitespace-nowrap sm:text-xs">
          {section.movies.length} film{section.movies.length !== 1 ? "s" : ""}
        </span>
      </div>

      {/* Scroll row with edge fades */}
      <div className="relative">
        {/* Fade gauche */}
        <div className="from-bg-primary pointer-events-none absolute top-0 bottom-0 left-0 z-10 w-8 bg-gradient-to-r to-transparent sm:w-12 md:w-16" />
        {/* Fade droite */}
        <div className="from-bg-primary pointer-events-none absolute top-0 right-0 bottom-0 z-10 w-8 bg-gradient-to-l to-transparent sm:w-12 md:w-16" />

        <div className="scrollbar-hide flex snap-x snap-proximity gap-2.5 overflow-x-auto scroll-smooth px-3 pb-3 sm:gap-3 sm:px-6 md:gap-3.5 md:px-12">
          {section.movies.map((movie) => (
            <div key={movie.id} className="snap-start">
              <div className="aspect-[2/3] h-100 flex-shrink-0">
                <MoviePoster movie={movie} size="original" />
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}

export function SectionRowSkeleton() {
  return (
    <section className="border-border border-t bg-amber-900 pt-5 pb-2 sm:pt-7">
      <div className="mb-3 flex items-center gap-3 px-3 sm:px-6 md:px-12">
        <div className="bg-surface-alt h-5 w-0.5 animate-pulse rounded-full" />
        <div className="bg-surface-alt h-4 w-36 animate-pulse rounded sm:h-5 sm:w-44" />
      </div>
      <div className="flex gap-2.5 bg-amber-600 px-3 pb-3 sm:gap-3 sm:px-6 md:gap-3.5 md:px-12">
        {Array.from({ length: 7 }).map((_, i) => (
          <div
            key={i}
            className="rounded-card bg-surface-alt aspect-[2/3] w-[104px] flex-shrink-0 animate-pulse sm:w-[120px] md:w-[152px]"
          />
        ))}
      </div>
    </section>
  );
}
