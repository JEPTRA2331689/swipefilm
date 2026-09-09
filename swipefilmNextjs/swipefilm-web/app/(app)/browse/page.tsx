"use client";

import { Suspense, useCallback, useEffect, useRef, useState } from "react";
import { useSearchParams } from "next/navigation";
import { useInView } from "react-intersection-observer";
import { Film } from "lucide-react";
import { TopNav } from "@/components/layout/TopNav";
import { MoviePoster } from "@/components/movie/MoviePoster";
import { Button } from "@/components/ui/Button";
import { browseSection, browseGenre } from "@/features/catalog/api";
import { cn } from "@/lib/utils";
import type { AvailabilityFilter, ContentTypeFilter, Movie } from "@/types";

const PAGE_SIZE = 24;

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

function parseAvailability(raw: string | null): AvailabilityFilter {
  const n = Number(raw);
  return n === 1 || n === 2 ? n : 0;
}

function parseContentType(raw: string | null): ContentTypeFilter {
  const n = Number(raw);
  return n === 1 || n === 2 ? n : 0;
}

function BrowseContent() {
  const params = useSearchParams();
  const sectionId = params.get("section");
  const genre = params.get("genre");
  const titleParam = params.get("title");
  // ✅ Initialisé depuis l'URL (conserve le filtre choisi sur la page
  // précédente) mais modifiable ici — état local, pas juste un dérivé.
  const [availability, setAvailability] = useState<AvailabilityFilter>(() =>
    parseAvailability(params.get("availability")),
  );
  // ✅ N'a de sens que pour une exploration par genre (films+séries mélangés)
  // — une section précise ("voir plus") est déjà d'un seul type.
  const [contentType, setContentType] = useState<ContentTypeFilter>(() =>
    parseContentType(params.get("contentType")),
  );

  const [movies, setMovies] = useState<Movie[]>([]);
  const [title, setTitle] = useState(titleParam ?? "");
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [hasMore, setHasMore] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchingRef = useRef(false);
  const shownIdsRef = useRef<Set<number>>(new Set());

  const fetchPage = useCallback(
    async (isFirst: boolean) => {
      if (fetchingRef.current) return;
      fetchingRef.current = true;
      if (isFirst) setLoading(true);
      else setLoadingMore(true);
      setError(null);

      try {
        const excludeIds = [...shownIdsRef.current];
        const res = sectionId
          ? await browseSection({
              sectionId,
              pageSize: PAGE_SIZE,
              availability,
              excludeIds,
            })
          : genre
            ? await browseGenre({
                genre,
                pageSize: PAGE_SIZE,
                availability,
                contentType,
                excludeIds,
              })
            : null;

        if (!res) {
          setError("Rien à explorer — section ou genre manquant.");
          return;
        }

        for (const m of res.movies) shownIdsRef.current.add(m.tmdbId);

        setMovies((prev) => (isFirst ? res.movies : [...prev, ...res.movies]));
        setHasMore(res.hasMore);
        if (isFirst) setTitle(res.title);
      } catch (e) {
        if (isFirst) setError(e instanceof Error ? e.message : "Erreur de chargement");
      } finally {
        fetchingRef.current = false;
        if (isFirst) setLoading(false);
        else setLoadingMore(false);
      }
    },
    [sectionId, genre, availability, contentType ],
  );

  useEffect(() => {
    setMovies([]);
    setHasMore(true);
    shownIdsRef.current = new Set();
    fetchPage(true);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sectionId, genre, availability, contentType]);

  const { ref, inView } = useInView({ threshold: 0, rootMargin: "400px" });

  useEffect(() => {
    if (inView && hasMore && !loading && !loadingMore) fetchPage(false);
  }, [inView, hasMore, loading, loadingMore, fetchPage]);

  return (
    <div className="bg-bg-primary min-h-screen">
      <TopNav />

      <div className="px-4 pt-8 pb-16 md:px-12">
        <h1 className="font-display text-text-primary mb-4 md:text-xl lg:text-4xl xl:text-6xl">
          {title || "Explorer"}
        </h1>

        <div className="mb-6 flex flex-col gap-2">
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

          {/* ✅ N'a de sens qu'en exploration par genre — une section précise
          est déjà d'un seul type (films OU séries). */}
          {genre && (
            <div className="ml-2 flex gap-1.5">
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
          )}
        </div>

        {error && (
          <p className="text-error py-8 text-center text-sm">{error}</p>
        )}

        {loading && (
          <div className="flex flex-wrap gap-4">
            {Array.from({ length: 12 }).map((_, i) => (
              <div
                key={i}
                className="bg-surface-alt aspect-[2/3] w-[152px] flex-shrink-0 animate-pulse"
              />
            ))}
          </div>
        )}

        {!loading && !error && movies.length === 0 && (
          <div className="text-text-secondary/60 flex flex-col items-center justify-center gap-2 py-16">
            <Film className="size-8" aria-hidden />
            <p className="text-sm">Rien à afficher pour l&apos;instant.</p>
          </div>
        )}

        {!loading && movies.length > 0 && (
          <div className="flex flex-wrap gap-4">
            {movies.map((m, i) => (
              <MoviePoster key={`${m.contentType}-${m.id}-${i}`} size="md" movie={m} />
            ))}
          </div>
        )}

        {loadingMore && (
          <div className="mt-4 flex flex-wrap gap-4">
            {Array.from({ length: 6 }).map((_, i) => (
              <div
                key={`more-${i}`}
                className="bg-surface-alt aspect-[2/3] w-[152px] flex-shrink-0 animate-pulse"
              />
            ))}
          </div>
        )}

        {!loading && hasMore && <div ref={ref} className="h-1" aria-hidden />}
      </div>
    </div>
  );
}

export default function BrowsePage() {
  return (
    <Suspense fallback={null}>
      <BrowseContent />
    </Suspense>
  );
}
