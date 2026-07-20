"use client";

import { useState } from "react";
import { TopNav } from "@/components/layout/TopNav";
import { SectionRow, SectionRowSkeleton } from "@/components/movie/SectionRow";
import { AutoCarousel} from "@/components/movie/HeaderCarrousel";
import { useHomeSections } from "@/hooks/useHomeSections";
import { useAppStore } from "@/lib/store";
import { cn } from "@/lib/utils";
import type { AvailabilityFilter } from "@/types";

const filters: { value: AvailabilityFilter; label: string }[] = [
  { value: "All", label: "Tous" },
  { value: "AvailableOnly", label: "Disponibles" },
  { value: "UnavailableOnly", label: "À découvrir" },
];

export default function HomePage() {
  const activeServer = useAppStore((s) => s.activeServer);
  const [availability, setAvailability] = useState<AvailabilityFilter>("All");

  const { sections, loading, error } = useHomeSections(
    activeServer?.id ?? null,
    availability
  );

  return (
    <div className="min-h-screen ">
      <TopNav />

      <div className="px-4 pt-10 pb-4 md:px-12 ">
        <AutoCarousel movies={sections[0]?.movies || []} />

        <div className="mt-5 flex gap-2 ">
          {filters.map((f) => (
            <button
              key={f.value}
              onClick={() => setAvailability(f.value)}
              className={cn(
                "rounded-pill border px-4 py-1.5 text-[13px] font-medium transition-colors",
                availability === f.value
                  ? "border-accent bg-accent/15 text-accent"
                  : "border-border text-text-secondary hover:text-text-primary"
              )}
            >
              {f.label}
            </button>
          ))}
        </div>
      </div>

      {error && (
        <p className="px-4 py-8 text-center text-sm text-error md:px-12">
          Impossible de charger les recommandations — {error}
        </p>
      )}

      {loading &&
        Array.from({ length: 4 }).map((_, i) => <SectionRowSkeleton key={i} />)}

      {!loading &&
        sections.map((section) => (
          <SectionRow key={section.id} section={section} />
        ))}

      {!loading && !error && sections.length === 0 && (
        <p className="px-4 py-16 text-center text-sm text-text-secondary md:px-12">
          Aucune section disponible pour l&apos;instant — swipez quelques films
          pour entraîner votre profil.
        </p>
      )}
    </div>
  );
}
