"use client";

import { useState, useEffect } from "react";
import Image from "next/image";
import { useRouter } from "next/navigation";
import { motion } from "framer-motion";
import { ArrowLeft, CircleCheck, PlusCircle, PlayCircle, Star } from "lucide-react";
import { TopNav } from "@/components/layout/TopNav";
import { useSeriesDetail } from "@/hooks/useSeriesDetail";
import { useSeriesSeasons } from "@/hooks/useSeriesSeasons";
import { useStaticRouteId } from "@/hooks/useStaticRouteId";
import { api } from "@/lib/api";
import { cn, releaseYear, tmdbImage } from "@/lib/utils";
import { createRequest, getRequestBySeries } from "@/features/catalog/api";
import { useAuth } from "@/features/auth/AuthContext";
import { Permission } from "@/lib/permissions";
import {
  SeriesRequestModal,
  type SeriesRequestSubmitOptions,
} from "@/features/catalog/components/SeriesRequestModal";
import { SimilarMedia } from "@/features/catalog/components/SimilarMedia";
import { Button, ButtonLink } from "@/components/ui/Button";
import { AvailabilityDot, Pill } from "@/components/ui/Badges";
import { SwipeActionButtons } from "@/components/movie/SwipeActionButtons";
import type { SeriesSeason } from "@/types";

export default function SeriesDetailClient() {
  const id = useStaticRouteId();
  const router = useRouter();
  const { series, loading, error } = useSeriesDetail(id);
  const { seasons, loading: seasonsLoading } = useSeriesSeasons(id);
  const { hasPermission } = useAuth();
  const canRequest = hasPermission(Permission.CanRequest);

  const [swipeState, setSwipeState] = useState<"idle" | "liked" | "disliked">(
    "idle",
  );
  const [requesting, setRequesting] = useState(false);
  const [requested, setRequested] = useState(false);
  const [showRequestModal, setShowRequestModal] = useState(false);

  async function handleSwipe(direction: "Right" | "Left") {
    if (!series) return;
    setSwipeState(direction === "Right" ? "liked" : "disliked");
    try {
      await api.post("/api/swipe", {
        seriesId: series.id,
        direction: direction === "Right" ? 1 : 0,
        durationMs: 2000,
        context: 0,
      });
    } catch {
      /* silencieux */
    }
  }

  async function handleRequest(opts: SeriesRequestSubmitOptions) {
    if (!series) return;
    setRequesting(true);
    try {
      await createRequest({
        seriesId: series.id,
        seasonNumbers:
          opts.seasonNumbers.length > 0 ? opts.seasonNumbers : undefined,
        qualityProfileId: opts.qualityProfileId,
        rootFolderPath: opts.rootFolderPath,
      });
      setRequested(true);
      setShowRequestModal(false);
    } catch {
      /* silencieux */
    } finally {
      setRequesting(false);
    }
  }

  // Vérifie si une requête existe déjà pour cette série
  useEffect(() => {
    if (!series?.tmdbId) return;
    getRequestBySeries(series.tmdbId).then((r) => {
      // status: 0=Pending 1=Approved 2=Downloading 3=PartiallyAvailable 4=Available 5=Declined
      if (r && r.status !== undefined && r.status !== 5) setRequested(true);
    });
  }, [series?.tmdbId]);

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

  if (error || !series) {
    return (
      <div className="bg-bg-primary min-h-screen">
        <TopNav />
        <div className="flex min-h-[60vh] flex-col items-center justify-center gap-4">
          <p className="text-text-secondary">{error ?? "Série introuvable"}</p>
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

  const poster = tmdbImage(series.posterPath, "w342");
  const backdrop = series.backdropPath
    ? tmdbImage(series.backdropPath, "original")
    : null;
  const year = releaseYear(series.firstAirDate);

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
                  alt={series.title}
                  fill
                  sizes="fill"
                  className="object-cover"
                />
              </div>
            </div>
          )}

          {/* Titre + sous-titre + actions */}
          <div className="min-w-0 flex-1 pt-3">
            <div className="flex flex-wrap items-center gap-3">
              <h1 className="font-display text-text-primary text-2xl leading-tight font-bold tracking-wide uppercase sm:text-3xl md:text-6xl">
                {series.title}
              </h1>
              {series.certification && (
                <Pill className="rounded-input font-semibold">
                  {series.certification}
                </Pill>
              )}
            </div>

            {/* Actions inline */}
            <div className="mt-3 flex flex-wrap gap-2 p-3">
              <SwipeActionButtons
                swipeState={swipeState}
                onSwipe={handleSwipe}
                className="text-fluid-label"
              />

              {!series.isAvailable && canRequest && (
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

              {series.jellyfinUrl && (
                <ButtonLink
                  variant="secondary"
                  href={series.jellyfinUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-fluid-label rounded-button border-swipe-star/40 bg-swipe-star/10 text-swipe-star hover:bg-swipe-star/20 gap-1.5 px-4 py-1.5"
                >
                  <PlayCircle className="size-4" />
                  Ouvrir dans Jellyfin
                </ButtonLink>
              )}

              {series.plexUrl && (
                <ButtonLink
                  variant="secondary"
                  href={series.plexUrl}
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
                  <p className="font-body text-fluid-heading text-text-primary mb-4 font-semibold">
                    details
                  </p>
                  <div className="space-y-3">
                    <DetailRow label="Année">{year ?? "—"}</DetailRow>
                    <DetailRow label="Saisons">
                      {series.numberOfSeasons > 0
                        ? series.numberOfSeasons
                        : "—"}
                    </DetailRow>
                    <DetailRow label="Épisodes">
                      {series.numberOfEpisodes > 0
                        ? series.numberOfEpisodes
                        : "—"}
                    </DetailRow>
                    <DetailRow label="Note TMDB">
                      <span className="flex items-center gap-1">
                        <RatingStars rating={series.tmdbRating} />
                        <span className="text-text-secondary ml-1 text-xs">
                          {series.tmdbRating.toFixed(1)}/10
                        </span>
                      </span>
                    </DetailRow>
                    <DetailRow label="Statut">
                      <Pill
                        variant={
                          series.isAvailable ? "available" : "unavailable"
                        }
                      >
                        {series.isAvailable ? "Disponible" : "Non disponible"}
                      </Pill>
                    </DetailRow>
                    {series.genres?.length > 0 && (
                      <DetailRow label="Genres">
                        <div className="flex flex-wrap gap-1.5">
                          {series.genres.map((g) => (
                            <Pill key={g} variant="accent">
                              {g}
                            </Pill>
                          ))}
                        </div>
                      </DetailRow>
                    )}
                    {series.createdBy?.length > 0 && (
                      <DetailRow label="Créateurs">
                        {series.createdBy.join(", ")}
                      </DetailRow>
                    )}
                  </div>
                </div>

                {/* Colonne Cast */}
                {series.castTop10?.length > 0 && (
                  <div className="w-1/2">
                    <p className="font-body text-fluid-heading text-text-primary mb-4 font-semibold">
                      cast
                    </p>
                    <div className="space-y-3">
                      {series.castTop10.slice(0, 5).map((name) => (
                        <div key={name} className="flex items-center gap-2.5">
                          <div className="size-fluid-avatar bg-surface border-border/60 relative aspect-square flex-shrink-0 overflow-hidden rounded-full border shadow-sm">
                            <div className="text-text-secondary flex h-full items-center justify-center text-xs font-medium">
                              {name[0]}
                            </div>
                          </div>
                          <p className="text-fluid-cast text-text-primary truncate leading-tight font-medium">
                            {name}
                          </p>
                        </div>
                      ))}
                    </div>
                  </div>
                )}
                <div className="xl:bg-border/40 flex-none lg:h-0.5 xl:w-1/2" />
              </div>
              <div className="border-border/40 mt-8 border-t" />
            </div>
          </div>
        </div>

        {/* ── Storyline ── */}
        {series.overview && (
          <div className="mt-8">
            <p className="font-display text-fluid-heading text-text-primary mb-3 ">
              Storyline
            </p>
            <p className="text-fluid-body text-text-secondary max-w-6xl leading-relaxed">
              {series.overview}
            </p>
          </div>
        )}

        {/* ── Saisons ── */}
        {(seasonsLoading || seasons.length > 0) && (
          <div className="mt-8">
            <p className="font-display text-fluid-heading text-text-primary mb-4 ">
              Saisons
            </p>

            {seasonsLoading ? (
              <div className="text-text-secondary flex items-center gap-2 text-sm">
                <div className="border-accent h-4 w-4 animate-spin rounded-full border-2 border-t-transparent" />
                Chargement des saisons…
              </div>
            ) : (
              <div className="flex scrollbar-none gap-3 overflow-x-auto pb-2">
                {seasons.map((season) => (
                  <SeasonPosterCard
                    key={season.seasonNumber}
                    season={season}
                    fallbackPoster={poster}
                    onClick={
                      canRequest ? () => setShowRequestModal(true) : undefined
                    }
                  />
                ))}
              </div>
            )}
          </div>
        )}

        {/* ── Séries similaires ── */}
        <SimilarMedia id={series.id} title={series.title} contentType="series" />

        <div className="border-border/40 mt-8 border-t pb-20" />
      </div>

      {showRequestModal && (
        <SeriesRequestModal
          series={series}
          seasons={seasons}
          seasonsLoading={seasonsLoading}
          submitting={requesting}
          onSubmit={handleRequest}
          onClose={() => setShowRequestModal(false)}
        />
      )}
    </div>
  );
}

/* ── Composants internes ── */

function SeasonPosterCard({
  season,
  fallbackPoster,
  onClick,
}: {
  season: SeriesSeason;
  fallbackPoster: string | null;
  onClick?: () => void;
}) {
  const poster = tmdbImage(season.posterPath, "w342") ?? fallbackPoster;

  return (
    <motion.div
      whileHover={onClick ? { y: -4 } : undefined}
      transition={{ duration: 0.18 }}
      onClick={onClick}
      className={cn("w-[130px] flex-shrink-0", onClick && "cursor-pointer")}
    >
      <div className="rounded-card bg-surface-alt relative aspect-[2/3] overflow-hidden">
        {poster ? (
          <Image
            src={poster}
            alt={`Saison ${season.seasonNumber}`}
            fill
            sizes="130px"
            className="object-cover"
          />
        ) : (
          <div className="text-text-secondary flex h-full items-center justify-center p-2 text-center text-xs">
            Saison {season.seasonNumber}
          </div>
        )}

        <div className="absolute inset-0 bg-gradient-to-t from-black/50 via-transparent to-transparent" />

        {/* ✅ Même badge de dispo que MoviePoster — dot en haut à gauche */}
        <div className="absolute top-2 left-2">
          <AvailabilityDot isAvailable={season.isAvailable} />
        </div>
      </div>

      <p className="text-text-primary mt-2 text-center text-xs font-medium">
        Saison {season.seasonNumber}
      </p>
    </motion.div>
  );
}

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
