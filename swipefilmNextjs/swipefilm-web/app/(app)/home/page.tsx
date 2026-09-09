"use client";

import { useEffect, useState } from "react";
import { useInView } from "react-intersection-observer";
import { TopNav } from "@/components/layout/TopNav";
import { SectionRow, SectionRowSkeleton } from "@/components/movie/SectionRow";
import { AutoCarousel } from "@/components/movie/HeaderCarrousel";
import { GenreCarousel } from "@/features/catalog/components/GenreCarousel";
import { CreateSessionButton } from "@/features/session/components/CreateSessionButton";
import { useHomeSections } from "@/hooks/useHomeSections";
import { useAppStore } from "@/lib/store";
import { cn, interleave } from "@/lib/utils";
import { Button } from "@/components/ui/Button";
import type { AvailabilityFilter, ContentTypeFilter } from "@/types";

const AVAILABILITY_FILTERS: { value: AvailabilityFilter; label: string }[] = [
  { value: 0, label: "Tous" },
  { value: 1, label: "Disponibles" },
  { value: 2, label: "À découvrir" },
];

const CONTENT_FILTERS: { value: ContentTypeFilter; label: string }[] = [
  { value: 0, label: "Films & Séries" },
  { value: 1, label: "Films" },
  { value: 2, label: "Séries" },
];

export default function HomePage() {
  const user = useAppStore((s) => s.user);
  const [availability, setAvailability] = useState<AvailabilityFilter>(0);
  const [contentType, setContentType] = useState<ContentTypeFilter>(0);

  const { sections, loading, loadingMore, error, hasMore, loadMore } =
    useHomeSections(!!user, availability, contentType);

  const { ref, inView } = useInView({ threshold: 0, rootMargin: "400px" });

  useEffect(() => {
    if (inView && hasMore && !loadingMore) loadMore();
  }, [inView, hasMore, loadingMore, loadMore]);

  // ✅ "for_you" (films) et "for_you_series" ont le même poids (100, le plus
  // haut) donc sont toujours générées toutes les deux en page 1 — mais
  // "Récemment disponible" s'insère avant elles sans condition, donc on ne
  // peut pas se fier à sections[0]. On les cherche par id et on les
  // entrelace pour un carrousel mixte films+séries.
  const forYouMovies = sections.find((s) => s.id === "for_you")?.movies ?? [];
  const forYouSeries =
    sections.find((s) => s.id === "for_you_series")?.movies ?? [];
  const forYouMix = interleave(forYouMovies, forYouSeries);

  return (
    <div className="min-h-screen">
      <TopNav />

      <div className="px-4 pt-10 pb-4 md:px-12">
        <AutoCarousel movies={forYouMix} />

        <div className="mt-5 flex flex-wrap gap-3">
          {/* Filtre disponibilité */}
          <div className="flex gap-1.5">
            {AVAILABILITY_FILTERS.map((f) => (
              <Button
                key={f.value}
                variant="secondary"
                size="sm"
                onClick={() => setAvailability(f.value)}
                className={cn(
                  "rounded-pill px-4 py-1.5 text-[13px]",
                  availability === f.value &&
                    "border-accent bg-accent/15 text-accent",
                )}
              >
                {f.label}
              </Button>
            ))}
          </div>

          <div className="bg-border hidden w-px self-stretch sm:block" />

          {/* Filtre type de contenu */}
          <div className="flex gap-1.5">
            {CONTENT_FILTERS.map((f) => (
              <Button
                key={f.value}
                variant="secondary"
                size="sm"
                onClick={() => setContentType(f.value)}
                className={cn(
                  "rounded-pill px-4 py-1.5 text-[13px]",
                  contentType === f.value &&
                    "border-secondary bg-secondary/10 text-secondary",
                )}
              >
                {f.label}
              </Button>
            ))}
          </div>

          <div className="ml-auto">
            <CreateSessionButton />
          </div>
        </div>
      </div>

      <GenreCarousel availability={availability} />

      {error && (
        <p className="text-error px-4 py-8 text-center text-sm md:px-12">
          Impossible de charger les recommandations — {error}
        </p>
      )}

      {loading &&
        Array.from({ length: 4 }).map((_, i) => <SectionRowSkeleton key={i} />)}

      {!loading &&
        sections.map((section) => (
          <SectionRow
            key={section.id}
            section={section}
            availability={availability}
          />
        ))}

      {!loading && !error && sections.length === 0 && !hasMore && (
        <p className="text-text-secondary px-4 py-16 text-center text-sm md:px-12">
          Aucune section disponible pour l&apos;instant — swipez quelques films
          pour entraîner votre profil.
        </p>
      )}

      {loadingMore &&
        Array.from({ length: 2 }).map((_, i) => (
          <SectionRowSkeleton key={`more-${i}`} />
        ))}

      {!loading && hasMore && <div ref={ref} className="h-1" aria-hidden />}
    </div>
  );
}
