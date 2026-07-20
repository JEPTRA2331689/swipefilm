"use client";

import { use, useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { motion } from "framer-motion";
import { TopNav } from "@/components/layout/TopNav";
import { MovieCard } from "@/components/movie/MovieCard";
import { Button } from "@/components/ui/Button";
import { useMovieDetail } from "@/hooks/useMovieDetail";
import { api } from "@/lib/api";
import { useAppStore } from "@/lib/store";
import { cn, formatRuntime, releaseYear, tmdbImage } from "@/lib/utils";

export default function MovieDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = use(params);
  const router = useRouter();
  const activeServer = useAppStore((s) => s.activeServer);
  const { movie, similar, extra, loading, error } = useMovieDetail(id);

  const [swipeState, setSwipeState] = useState<"idle" | "liked" | "disliked">("idle");
  const [requesting, setRequesting] = useState(false);
  const [requested, setRequested] = useState(false);

  async function handleSwipe(direction: "Right" | "Left") {
    if (!movie) return;
    setSwipeState(direction === "Right" ? "liked" : "disliked");
    try {
      await api.post("/api/swipes", {
        movieId: movie.id,
        direction: direction === "Right" ? 1 : 0,
        durationMs: 2000,
        context: 0,
      });
    } catch { /* silencieux */ }
  }

  async function handleOverseerrRequest() {
    if (!movie) return;
    setRequesting(true);
    try {
      await api.post(`/api/seerr/request/${movie.tmdbId}`);
      setRequested(true);
    } catch { /* silencieux */ } finally {
      setRequesting(false);
    }
  }

  if (loading) {
    return (
      <div className="min-h-screen">
        <TopNav />
        <div className="flex items-center justify-center py-32">
          <div className="h-10 w-10 rounded-full border-2 border-accent border-t-transparent animate-spin" />
        </div>
      </div>
    );
  }

  if (error || !movie) {
    return (
      <div className="min-h-screen">
        <TopNav />
        <div className="flex flex-col items-center justify-center py-32 gap-4">
          <p className="text-text-secondary">{error ?? "Film introuvable"}</p>
          <Button variant="outline" onClick={() => router.back()}>
            ← Retour
          </Button>
        </div>
      </div>
    );
  }

  const poster = tmdbImage(movie.posterPath, "w500");
  const backdrop = extra?.backdropPath ?? null;
  const year = releaseYear(movie.releaseDate);
  const runtime = formatRuntime(movie.runtimeMinutes ?? null);

  return (
    <div className="min-h-screen">
      <TopNav />

      {/* ── Hero backdrop ── */}
      <div className="relative h-[55vw] max-h-[520px] min-h-[280px] overflow-hidden">
        {backdrop ? (
          <Image
            src={backdrop}
            alt=""
            fill
            sizes="100vw"
            className="object-cover object-top"
            priority
            aria-hidden
          />
        ) : poster ? (
          <Image
            src={poster}
            alt=""
            fill
            sizes="100vw"
            className="object-cover object-top blur-sm scale-110 opacity-60"
            priority
            aria-hidden
          />
        ) : null}

        {/* Gradients */}
        <div className="absolute inset-0 bg-gradient-to-t from-bg-primary via-bg-primary/40 to-transparent" />
        <div className="absolute inset-0 bg-gradient-to-r from-bg-primary/60 to-transparent" />

        {/* Bouton retour */}
        <button
          onClick={() => router.back()}
          className="absolute top-4 left-4 flex items-center gap-1.5 rounded-pill border border-border bg-bg-primary/60 px-3 py-1.5 text-sm text-text-secondary backdrop-blur-sm hover:text-text-primary transition-colors"
        >
          ← Retour
        </button>
      </div>

      {/* ── Contenu principal ── */}
      <div className="relative -mt-32 z-10 px-4 pb-16 md:px-12">
        <div className="flex gap-6 md:gap-10">

          {/* Poster */}
          {poster && (
            <div className="hidden sm:block flex-shrink-0 w-[160px] md:w-[200px]">
              <div className="relative aspect-[2/3] rounded-card overflow-hidden border border-border shadow-2xl">
                <Image
                  src={poster}
                  alt={movie.title}
                  fill
                  sizes="200px"
                  className="object-cover"
                />
              </div>
            </div>
          )}

          {/* Infos */}
          <div className="flex-1 min-w-0 pt-8 sm:pt-16">
            {extra?.tagline && (
              <p className="text-sm italic text-accent mb-2">{extra.tagline}</p>
            )}

            <h1 className="font-display text-2xl md:text-4xl font-bold text-text-primary leading-tight">
              {movie.title}
            </h1>

            {/* Meta */}
            <div className="mt-3 flex flex-wrap items-center gap-2 text-sm text-text-secondary">
              {year && <span>{year}</span>}
              {runtime && (
                <>
                  <span className="opacity-30">·</span>
                  <span>{runtime}</span>
                </>
              )}
              <span className="opacity-30">·</span>
              <span className="flex items-center gap-1">
                <span className="text-warning">★</span>
                <span className="font-semibold text-text-primary">
                  {movie.tmdbRating.toFixed(1)}
                </span>
                <span className="text-xs">/10</span>
              </span>
              {/* Disponibilité */}
              <span className={cn(
                "rounded-pill px-2 py-0.5 text-xs font-medium border",
                movie.isAvailable
                  ? "border-swipe-star/40 bg-swipe-star/10 text-swipe-star"
                  : "border-border bg-surface text-text-secondary"
              )}>
                {movie.isAvailable ? "Disponible" : "Non disponible"}
              </span>
            </div>

            {/* Genres */}
            {movie.genres?.length > 0 && (
              <div className="mt-3 flex flex-wrap gap-2">
                {movie.genres.map((g) => (
                  <span
                    key={g}
                    className="rounded-pill border border-accent/25 bg-accent/10 px-3 py-0.5 text-xs text-accent"
                  >
                    {g}
                  </span>
                ))}
              </div>
            )}

            {/* Réalisateurs */}
            {extra?.directors && extra.directors.length > 0 && (
              <p className="mt-3 text-xs text-text-secondary">
                <span className="text-text-primary font-medium">Réalisé par </span>
                {extra.directors.join(", ")}
              </p>
            )}

            {/* Actions */}
            <div className="mt-5 flex flex-wrap gap-3">
              <motion.button
                whileTap={{ scale: 0.95 }}
                onClick={() => handleSwipe("Right")}
                className={cn(
                  "flex items-center gap-2 rounded-button px-5 py-2.5 text-sm font-medium border transition-all",
                  swipeState === "liked"
                    ? "border-swipe-like bg-swipe-like/20 text-swipe-like"
                    : "border-swipe-like/40 bg-swipe-like/10 text-swipe-like hover:bg-swipe-like/20"
                )}
              >
                👍 {swipeState === "liked" ? "Aimé !" : "J'aime"}
              </motion.button>

              <motion.button
                whileTap={{ scale: 0.95 }}
                onClick={() => handleSwipe("Left")}
                className={cn(
                  "flex items-center gap-2 rounded-button px-5 py-2.5 text-sm font-medium border transition-all",
                  swipeState === "disliked"
                    ? "border-swipe-skip bg-swipe-skip/20 text-swipe-skip"
                    : "border-swipe-skip/40 bg-swipe-skip/10 text-swipe-skip hover:bg-swipe-skip/20"
                )}
              >
                👎 {swipeState === "disliked" ? "Noté !" : "Pas pour moi"}
              </motion.button>

              {!movie.isAvailable && (
                <motion.button
                  whileTap={{ scale: 0.95 }}
                  onClick={handleOverseerrRequest}
                  disabled={requesting || requested}
                  className={cn(
                    "flex items-center gap-2 rounded-button px-5 py-2.5 text-sm font-medium border transition-all",
                    requested
                      ? "border-accent/40 bg-accent/10 text-accent"
                      : "border-border bg-surface text-text-secondary hover:text-text-primary hover:border-accent/40"
                  )}
                >
                  {requested ? "✓ Demandé" : requesting ? "Envoi…" : "📥 Requêter"}
                </motion.button>
              )}
            </div>
          </div>
        </div>

        {/* ── Synopsis ── */}
        {movie.overview && (
          <div className="mt-8 md:mt-10">
            <h2 className="font-display text-lg font-semibold text-text-primary mb-3 flex items-center gap-3">
              <div className="h-5 w-0.5 rounded-full bg-accent opacity-70" />
              Synopsis
            </h2>
            <p className="text-[15px] leading-relaxed text-text-secondary max-w-3xl">
              {movie.overview}
            </p>
          </div>
        )}

        {/* ── Cast ── */}
        {extra?.cast && extra.cast.length > 0 && (
          <div className="mt-8 md:mt-10">
            <h2 className="font-display text-lg font-semibold text-text-primary mb-4 flex items-center gap-3">
              <div className="h-5 w-0.5 rounded-full bg-accent opacity-70" />
              Casting
            </h2>
            <div className="flex gap-4 overflow-x-auto pb-2">
              {extra.cast.map((actor) => (
                <div key={actor.name} className="flex-shrink-0 w-20 text-center">
                  <div className="relative w-16 h-16 mx-auto rounded-full overflow-hidden bg-surface-alt border border-border mb-2">
                    {actor.profilePath ? (
                      <Image
                        src={actor.profilePath}
                        alt={actor.name}
                        fill
                        sizes="64px"
                        className="object-cover"
                      />
                    ) : (
                      <div className="flex h-full items-center justify-center text-lg text-text-secondary">
                        {actor.name[0]}
                      </div>
                    )}
                  </div>
                  <p className="text-[11px] font-medium text-text-primary leading-tight line-clamp-2">
                    {actor.name}
                  </p>
                  <p className="text-[10px] text-text-secondary mt-0.5 line-clamp-1">
                    {actor.character}
                  </p>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* ── Films similaires ── */}
        {similar.length > 0 && (
          <div className="mt-10">
            <h2 className="font-display text-lg font-semibold text-text-primary mb-4 flex items-center gap-3">
              <div className="h-5 w-0.5 rounded-full bg-accent opacity-70" />
              Vous aimerez aussi
            </h2>
            <div className="relative">
              <div className="pointer-events-none absolute right-0 top-0 bottom-0 w-12 z-10 bg-gradient-to-l from-bg-primary to-transparent" />
              <div className="flex gap-3 overflow-x-auto pb-3">
                {similar.map((m) => (
                  <div key={m.id} className="snap-start">
                    <MovieCard movie={m} />
                  </div>
                ))}
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}