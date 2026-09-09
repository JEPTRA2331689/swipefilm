"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { Search, Film } from "lucide-react";
import { TopNav } from "@/components/layout/TopNav";
import { MoviePoster } from "@/components/movie/MoviePoster";
import { api } from "@/lib/api";

const DEBOUNCE_MS = 350;

interface SearchResultItem {
  id: string;
  tmdbId: number;
  title: string;
  posterPath: string | null;
  releaseDate: string | null;
  tmdbRating: number;
  contentType: "movie" | "series";
  isAvailable: boolean;
}

interface SearchResponse {
  results: SearchResultItem[];
  page: number;
  totalPages: number;
}

// ✅ Debounce + AbortController — même principe que le fix de survol du
// carrousel : sans ça, une réponse tardive (frappe rapide) peut écraser
// l'affichage d'une recherche plus récente.
export default function SearchPage() {
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<SearchResultItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [searched, setSearched] = useState(false);

  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const abortRef = useRef<AbortController | null>(null);

  const runSearch = useCallback(async (q: string) => {
    abortRef.current?.abort();

    if (!q.trim()) {
      setResults([]);
      setSearched(false);
      setLoading(false);
      return;
    }

    const controller = new AbortController();
    abortRef.current = controller;
    setLoading(true);

    try {
      const res = await api.get<SearchResponse>(
        `/api/search?q=${encodeURIComponent(q)}`,
        { signal: controller.signal },
      );
      if (!controller.signal.aborted) setResults(res.results);
    } catch {
      /* requête annulée ou erreur — silencieux */
    } finally {
      if (!controller.signal.aborted) {
        setLoading(false);
        setSearched(true);
      }
    }
  }, []);

  function handleChange(value: string) {
    setQuery(value);
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => runSearch(value), DEBOUNCE_MS);
  }

  useEffect(() => {
    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
      abortRef.current?.abort();
    };
  }, []);

  return (
    <div className="bg-bg-primary min-h-screen">
      <TopNav />

      <div className="mx-auto max-w-6xl px-4 pt-8 pb-16">
        <div className="relative mb-8">
          <Search
            className="size-5 text-text-secondary absolute top-1/2 left-4 -translate-y-1/2"
            aria-hidden
          />
          <input
            type="text"
            value={query}
            onChange={(e) => handleChange(e.target.value)}
            placeholder="Rechercher un film ou une série…"
            autoFocus
            className="rounded-input border-border bg-surface text-text-primary placeholder:text-text-secondary/60 focus:border-accent/50 w-full border py-3 pr-4 pl-12 text-sm transition-colors focus:outline-none"
          />
        </div>

        {loading && (
          <div className="flex items-center justify-center py-16">
            <div className="border-accent h-8 w-8 animate-spin rounded-full border-2 border-t-transparent" />
          </div>
        )}

        {!loading && searched && results.length === 0 && (
          <div className="text-text-secondary flex flex-col items-center justify-center gap-2 py-16">
            <Search className="size-8" aria-hidden />
            <p className="text-sm">Aucun résultat pour « {query} ».</p>
          </div>
        )}

        {!loading && !searched && results.length === 0 && (
          <div className="text-text-secondary/60 flex flex-col items-center justify-center gap-2 py-16">
            <Film className="size-8" aria-hidden />
            <p className="text-sm">
              Commence à taper pour chercher un film ou une série.
            </p>
          </div>
        )}

        {!loading && results.length > 0 && (
          <div className="flex flex-wrap gap-4">
            {results.map((r) => (
              <MoviePoster
                key={`${r.contentType}-${r.id}`}
                size="md"
                movie={{
                  id: r.id,
                  tmdbId: r.tmdbId,
                  title: r.title,
                  posterPath: r.posterPath,
                  backdropPath: null,
                  overview: null,
                  tmdbRating: r.tmdbRating,
                  runtimeMinutes: null,
                  genres: [],
                  releaseDate: r.releaseDate,
                  contentType: r.contentType,
                  isAvailable: r.isAvailable,
                }}
              />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}
