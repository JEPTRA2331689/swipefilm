"use client";

import { useState, useEffect } from "react";
import Image from "next/image";
import { useRouter } from "next/navigation";
import {
  ArrowLeft,
  CircleCheck,
  PlusCircle,
  PlayCircle,
  Star,
} from "lucide-react";
import { TopNav } from "@/components/layout/TopNav";
import { useMovieDetail } from "@/hooks/useMovieDetail";
import { useStaticRouteId } from "@/hooks/useStaticRouteId";
import { api } from "@/lib/api";
import { cn, formatRuntime, releaseYear, tmdbImage } from "@/lib/utils";
import { createRequest, getRequestByMovie } from "@/features/catalog/api";
import { useAuth } from "@/features/auth/AuthContext";
import { Permission } from "@/lib/permissions";
import {
  RequestOptionsModal,
  type RequestOptions,
} from "@/features/catalog/components/RequestOptionsModal";
import { SimilarMedia } from "@/features/catalog/components/SimilarMedia";
import { Button, ButtonLink } from "@/components/ui/Button";
import { Pill } from "@/components/ui/Badges";
import { SwipeActionButtons } from "@/components/movie/SwipeActionButtons";

export default function MovieDetailClient() {
  const id = useStaticRouteId();
  const router = useRouter();
  const { movie, extra, loading, error } = useMovieDetail(id);
  const { hasPermission } = useAuth();
  const canRequest = hasPermission(Permission.CanRequest);

  const [swipeState, setSwipeState] = useState<"idle" | "liked" | "disliked">(
    "idle",
  );
  const [requesting, setRequesting] = useState(false);
  const [requested, setRequested] = useState(false);
  const [showRequestModal, setShowRequestModal] = useState(false);

  async function handleSwipe(direction: "Right" | "Left") {
    if (!movie) return;
    setSwipeState(direction === "Right" ? "liked" : "disliked");
    try {
      await api.post("/api/swipe", {
        movieId: movie.id,
        direction: direction === "Right" ? 1 : 0,
        durationMs: 2000,
        context: 0,
      });
    } catch {
      /* silencieux */
    }
  }

  async function handleRequest(opts?: RequestOptions) {
    if (!movie) return;
    setRequesting(true);
    try {
      await createRequest({ movieId: movie.id, ...opts });
      setRequested(true);
      setShowRequestModal(false);
    } catch {
      /* silencieux */
    } finally {
      setRequesting(false);
    }
  }
  // Vérifie si une requête existe déjà pour ce film
  useEffect(() => {
    if (!movie?.tmdbId) return;
    getRequestByMovie(movie.tmdbId).then((r) => {
      // status: 0=Pending 1=Approved 2=Downloading 3=PartiallyAvailable 4=Available 5=Declined
      if (r && r.status !== undefined && r.status !== 5) setRequested(true);
    });
  }, [movie?.tmdbId]);

  if (loading) {
    return (
      <div className="bg-bg-primary min-h-screen">
        <TopNav />
        <div className="flex min-h-[60vh] items-center justify-center">
          <div className="border-accent h-8 w-8 animate-spin rounded-full border-2 border-t-transparent" />
        </div>
      </div>
    );
  }

  if (error || !movie) {
    return (
      <div className="bg-bg-primary min-h-screen">
        <TopNav />
        <div className="flex min-h-[60vh] flex-col items-center justify-center gap-4">
          <p className="text-text-secondary">{error ?? "Film introuvable"}</p>
          <button
            onClick={() => router.back()}
            className="text-accent text-sm hover:underline"
          >
            ← Retour
          </button>
        </div>
      </div>
    );
  }

  const poster = tmdbImage(movie.posterPath, "w342");
  const backdrop = extra?.backdropPath ?? null;
  const year = releaseYear(movie.releaseDate);
  const runtime = formatRuntime(movie.runtimeMinutes ?? null);

  return (
    <div className="bg-bg-primary min-h-screen">
      <TopNav />

      {/* ── Hero backdrop plein-écran ── */}
      <div className="bg-surface relative h-[52vw] max-h-[700px] min-h-[300px] w-full overflow-hidden">
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
            className="scale-110 object-cover object-top opacity-40 blur-md"
            priority
            aria-hidden
          />
        ) : (
          <div className="bg-surface absolute inset-0" />
        )}
        <div className="to-bg-primary absolute inset-0 bg-gradient-to-b from-transparent via-transparent" />

        {/* Bouton retour */}
        <button
          onClick={() => router.back()}
          className="absolute top-4 left-4 z-10 flex items-center gap-1.5 text-sm text-white/80 transition-colors hover:text-white"
        >
          <ArrowLeft className="size-4" aria-hidden />
          Retour
        </button>
      </div>

      {/* ── Section identité ── */}
      <div className="p- -mt-30 px-8 sm:px-60">
        <div className="relative z-10 flex items-start gap-5 md:gap-10 lg:gap-20">
          {/* Poster */}
          {poster && (
            <div className="w-1/6 flex-shrink-0">
              <div className="relative aspect-[2/3] overflow-hidden border border-white/10 shadow-xl">
                <Image
                  src={poster}
                  alt={movie.title}
                  fill
                  sizes="fill"
                  className="object-cover"
                />
              </div>
            </div>
          )}

          {/* Titre + sous-titre + actions */}
          <div className="min-w-0 flex-1 pt-3">
            <h1 className="font-display text-text-primary text-2xl leading-tight font-bold tracking-wide uppercase sm:text-3xl md:text-6xl">
              {movie.title}
            </h1>

            {/* Actions inline */}
            <div className="mt-3 flex flex-wrap gap-2 p-3">
              <SwipeActionButtons
                swipeState={swipeState}
                onSwipe={handleSwipe}
                className="text-fluid-label"
              />

              {!movie.isAvailable && canRequest && (
                <Button
                  variant={requested ? "accent-soft" : "secondary"}
                  onClick={() => setShowRequestModal(true)}
                  disabled={requesting || requested}
                  className="text-fluid-label rounded-button gap-1.5 px-4 py-1.5"
                >
                  {requested ? (
                    <CircleCheck className="size-4" />
                  ) : (
                    <PlusCircle className="size-4" />
                  )}
                  {requested ? "Demandé" : requesting ? "Envoi…" : "Requêter"}
                </Button>
              )}

              {movie.isAvailable && movie.jellyfinUrl && (
                <ButtonLink
                  variant="secondary"
                  href={movie.jellyfinUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-fluid-label rounded-button border-swipe-star/40 bg-swipe-star/10 text-swipe-star hover:bg-swipe-star/20 gap-1.5 px-4 py-1.5"
                >
                  <PlayCircle className="size-4" />
                  Ouvrir dans Jellyfin
                </ButtonLink>
              )}

              {movie.isAvailable && movie.plexUrl && (
                <ButtonLink
                  variant="secondary"
                  href={movie.plexUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-fluid-label rounded-button border-swipe-star/40 bg-swipe-star/10 text-swipe-star hover:bg-swipe-star/20 gap-1.5 px-4 py-1.5"
                >
                  <PlayCircle className="size-4" />
                  Ouvrir dans Plex
                </ButtonLink>
              )}
            </div>
            <div className=" ">
              <div className="border-border/40 mt-6 border-t" />

              {/* ── Details + Cast ── */}
              <div className="mt-6 flex max-h-[600px] gap-8">
                {/* Colonne Details */}
                <div className="w-1/2">
                  <p className="text-fluid-heading text-text-primary mb-4 font-semibold">
                    details
                  </p>
                  <div className="space-y-3">
                    {/* Année + Durée */}
                    <DetailRow label="Année">{year ?? "—"}</DetailRow>
                    <DetailRow label="Durée">{runtime ?? "—"}</DetailRow>
                    {/* Note */}
                    <DetailRow label="Note TMDB">
                      <span className="flex items-center gap-1">
                        <RatingStars rating={movie.tmdbRating} />
                        <span className="text-text-secondary ml-1 text-xs">
                          {movie.tmdbRating.toFixed(1)}/10
                        </span>
                      </span>
                    </DetailRow>
                    {/* Disponibilité */}
                    <DetailRow label="Statut">
                      <Pill
                        variant={
                          movie.isAvailable ? "available" : "unavailable"
                        }
                      >
                        {movie.isAvailable ? "Disponible" : "Non disponible"}
                      </Pill>
                    </DetailRow>
                    {/* Genres */}
                    {movie.genres?.length > 0 && (
                      <DetailRow label="Genres">
                        <div className="flex flex-wrap gap-1.5">
                          {movie.genres.map((g) => (
                            <Pill key={g} variant="accent">
                              {g}
                            </Pill>
                          ))}
                        </div>
                      </DetailRow>
                    )}
                    {/* Réalisateur */}
                    {extra?.directors && extra.directors.length > 0 && (
                      <DetailRow label="Réalisateur">
                        {extra.directors.join(", ")}
                      </DetailRow>
                    )}
                  </div>
                </div>

                {/* Colonne Cast */}
                {extra?.cast && extra.cast.length > 0 && (
                  <div className="w-1/2">
                    <p className="text-fluid-heading text-text-primary mb-4 font-semibold">
                      cast
                    </p>
                    <div className="space-y-3">
                      {extra.cast.slice(0, 5).map((actor) => (
                        <div
                          key={actor.name}
                          className="flex items-center gap-2.5"
                        >
                          <div className="size-fluid-avatar bg-surface border-border/60 relative aspect-square flex-shrink-0 overflow-hidden rounded-full border shadow-sm">
                            {actor.profilePath ? (
                              <Image
                                src={actor.profilePath}
                                alt={actor.name}
                                fill
                                sizes="fill"
                                className="object-cover"
                              />
                            ) : (
                              <div className="text-text-secondary flex h-full items-center justify-center text-xs font-medium">
                                {actor.name[0]}
                              </div>
                            )}
                          </div>
                          <div className="">
                            <p className="text-fluid-cast text-text-primary truncate leading-tight font-medium">
                              {actor.name}
                            </p>
                            {actor.character && (
                              <p className="text-fluid-cast text-text-secondary truncate italic">
                                {actor.character}
                              </p>
                            )}
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                )}
                <div className="xl:bg-border/40 flex-none lg:h-0.5 xl:w-1/2" />
              </div>
              {/* ── Ligne séparatrice ── */}
              <div className="border-border/40 mt-8 border-t" />
            </div>
          </div>
        </div>

        {/* ── Ligne séparatrice ── */}

        {/* ── Storyline ── */}
        {movie.overview && (
          <div className="mt-8">
            <p className="text-fluid-heading text-text-primary mb-3 font-semibold">
              Storyline
            </p>
            <p className="text-fluid-body text-text-secondary max-w-6xl leading-relaxed">
              {movie.overview}
            </p>
          </div>
        )}

        {/* ── Ligne séparatrice ── */}
        <div className="border-border/40 mt-8 border-t" />

        {/* ── Films similaires ── */}
        {movie && (
          <SimilarMedia id={movie.id} title={movie.title} contentType="movie" />
        )}
      </div>

      {showRequestModal && (
        <RequestOptionsModal
          contentType="movie"
          title={`Requêter « ${movie.title} »`}
          submitting={requesting}
          onConfirm={handleRequest}
          onClose={() => setShowRequestModal(false)}
        />
      )}
    </div>
  );
}

/* ── Composants internes ── */

function DetailRow({
  label,
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div className="flex items-start gap-3 p-2">
      <h1 className="text-fluid-label text-text-secondary flex-shrink-0 pt-0.5 tracking-wider uppercase">
        {label}
      </h1>
      <h1 className="text-fluid-value text-text-primary flex-1">{children}</h1>
    </div>
  );
}

function RatingStars({ rating }: { rating: number }) {
  const filled = Math.round(rating / 2);
  return (
    <span className="flex gap-0.5">
      {Array.from({ length: 5 }).map((_, i) => (
        <Star
          key={i}
          className={cn(
            "size-3.5",
            i < filled ? "text-warning" : "text-border",
          )}
          aria-hidden
        />
      ))}
    </span>
  );
}

