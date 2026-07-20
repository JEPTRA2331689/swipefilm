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
    <section className="border-t border-border pt-5 pb-2 sm:pt-7 ">
      <div className="flex items-baseline justify-between gap-2 px-3 mb-3 sm:gap-3 sm:px-6 md:px-12">
        <div className="flex items-center gap-3">
          {/* Accent bar */}
          <div className="h-5 w-0.5 rounded-full bg-accent opacity-70" />
          <h2 className="font-display text-base font-semibold text-text-primary sm:text-[17px] lg:text-[19px]">
            {section.title}
          </h2>
        </div>
        <span className="text-[11px] text-text-secondary whitespace-nowrap sm:text-xs">
          {section.movies.length} film{section.movies.length !== 1 ? "s" : ""}
        </span>
      </div>

      {/* Scroll row with edge fades */}
      <div className="relative">
        {/* Fade gauche */}
        <div className="pointer-events-none absolute left-0 top-0 bottom-0 w-8 z-10 bg-gradient-to-r from-bg-primary to-transparent sm:w-12 md:w-16" />
        {/* Fade droite */}
        <div className="pointer-events-none absolute right-0 top-0 bottom-0 w-8 z-10 bg-gradient-to-l from-bg-primary to-transparent sm:w-12 md:w-16" />

        <div className="flex gap-2.5 overflow-x-auto px-3 pb-3 scroll-smooth snap-x snap-proximity scrollbar-hide sm:gap-3 sm:px-6 md:gap-3.5 md:px-12">
          {section.movies.map((movie) => (
            <div key={movie.id} className="snap-start">
              <div className="flex-shrink-0 h-100 aspect-[2/3]">
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
    <section className="border-t border-border pt-5 pb-2 sm:pt-7 bg-amber-900">
      <div className="flex items-center gap-3 px-3 mb-3 sm:px-6 md:px-12 ">
        <div className="h-5 w-0.5 rounded-full bg-surface-alt animate-pulse " />
        <div className="h-4 w-36 rounded bg-surface-alt animate-pulse sm:h-5 sm:w-44 " />
      </div>
      <div className="flex gap-2.5 px-3 pb-3 sm:gap-3 sm:px-6 md:gap-3.5 md:px-12 bg-amber-200">
        {Array.from({ length: 7 }).map((_, i) => (
          <div
            key={i}
            className="w-[104px] flex-shrink-0 aspect-[2/3] rounded-card bg-surface-alt animate-pulse sm:w-[120px] md:w-[152px]"
          />
        ))}
      </div>
    </section>
  );
}