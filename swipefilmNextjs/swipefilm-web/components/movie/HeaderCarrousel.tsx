"use client";

import React, { useCallback, useEffect, useRef, useState } from "react";
import useEmblaCarousel from "embla-carousel-react";
import Autoplay from "embla-carousel-autoplay";
import Image from "next/image";
import Link from "next/link";
import { api } from "@/lib/api";
import type { Movie } from "@/types";
import { releaseYear, tmdbImage, cn } from "@/lib/utils";
import { AvailabilityDot } from "@/components/ui/Badges";
import { IconButton } from "@/components/ui/Button";

const TRAILER_HOVER_DELAY = 4000;

interface AutoCarouselProps {
  movies: Movie[];
  delay?: number;
  className?: string;
}

// ✅ Isolé dans son propre composant avec sa propre "clé" (selectedIndex) —
// seule cette jauge remonte/rerender au changement de slide ou pause,
// jamais tout AutoCarousel (cartes, images, liens...).
function ProgressBar({
  duration,
  paused,
}: {
  duration: number;
  paused: boolean;
}) {
  return (
    <div className="bg-border h-0.5 w-32 overflow-hidden rounded-full">
      <div
        className="bg-accent h-full rounded-full"
        style={{
          animationName: "progress-fill",
          animationDuration: `${duration}ms`,
          animationTimingFunction: "linear",
          animationFillMode: "forwards",
          animationPlayState: paused ? "paused" : "running",
        }}
      />
    </div>
  );
}

export function AutoCarousel({
  movies,
  delay = 4000,
  className,
}: AutoCarouselProps) {
  const [selectedIndex, setSelectedIndex] = useState(0);
  const [isPaused, setIsPaused] = useState(false);

  // ✅ Bande-annonce au survol prolongé (4s) — récupérée à la demande et
  // mise en cache côté backend (TmdbService), jamais préchargée pour tout
  // le carrousel d'un coup.
  const [trailerCardId, setTrailerCardId] = useState<string | null>(null);
  const [trailerKey, setTrailerKey] = useState<string | null>(null);
  const [trailerMuted, setTrailerMuted] = useState(true);
  const trailerTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const trailerIframeRef = useRef<HTMLIFrameElement>(null);
  const trailerAbortRef = useRef<AbortController | null>(null);
  // ✅ Évite de refetch la même bande-annonce à chaque survol répété de la
  // même carte — null = déjà vérifié, aucune bande-annonce disponible.
  const trailerCacheRef = useRef<Map<string, string | null>>(new Map());
  const mountedRef = useRef(true);

  useEffect(() => {
    mountedRef.current = true;
    return () => {
      mountedRef.current = false;
    };
  }, []);

  // ✅ Coupe la vidéo YouTube avant de démonter l'iframe — sinon le son
  // continue quelques centaines de ms après la disparition de l'iframe.
  const stopTrailerPlayback = useCallback(() => {
    trailerIframeRef.current?.contentWindow?.postMessage(
      JSON.stringify({ event: "command", func: "stopVideo", args: [] }),
      "*",
    );
  }, []);

  const clearTrailerTimer = useCallback(() => {
    if (trailerTimerRef.current) clearTimeout(trailerTimerRef.current);
    trailerTimerRef.current = null;
    // ✅ Annule aussi le fetch en vol — sans ça, clearTimeout n'a plus prise
    // une fois le timer sonné, et une réponse tardive (survol A→B→C rapide)
    // peut écraser l'état d'une carte plus récente.
    trailerAbortRef.current?.abort();
    trailerAbortRef.current = null;
  }, []);

  const handleCardMouseEnter = useCallback(
    (movie: Movie) => {
      clearTrailerTimer();

      trailerTimerRef.current = setTimeout(async () => {
        const cached = trailerCacheRef.current.get(movie.id);
        if (cached !== undefined) {
          if (cached) {
            setTrailerMuted(true);
            setTrailerKey(cached);
            setTrailerCardId(movie.id);
          }
          return;
        }

        const controller = new AbortController();
        trailerAbortRef.current = controller;

        try {
          const path =
            movie.contentType === "series"
              ? `/api/series/${movie.id}/trailer`
              : `/api/movies/${movie.id}/trailer`;
          const res = await api.get<{ youtubeKey: string | null }>(path, {
            signal: controller.signal,
          });

          trailerCacheRef.current.set(movie.id, res.youtubeKey);

          // ✅ Composant démonté ou requête annulée entre-temps — ne touche
          // plus au state (unmount) ni à un affichage qui n'est plus valide.
          if (!mountedRef.current || controller.signal.aborted) return;

          if (res.youtubeKey) {
            setTrailerMuted(true);
            setTrailerKey(res.youtubeKey);
            setTrailerCardId(movie.id);
          }
        } catch {
          /* silencieux — pas de bande-annonce ou requête annulée, on garde le backdrop */
        }
      }, TRAILER_HOVER_DELAY);
    },
    [clearTrailerTimer],
  );

  const handleCardMouseLeave = useCallback(() => {
    clearTrailerTimer();
    stopTrailerPlayback();
    setTrailerCardId(null);
    setTrailerKey(null);
  }, [clearTrailerTimer, stopTrailerPlayback]);

  useEffect(() => clearTrailerTimer, [clearTrailerTimer]);

  // ✅ Coupe/rétablit le son via l'API postMessage YouTube (enablejsapi=1)
  // au lieu de recharger l'iframe avec un nouveau paramètre mute — ça
  // couperait la lecture et redémarrerait la vidéo depuis le début.
  const toggleTrailerMute = useCallback((e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setTrailerMuted((prevMuted) => {
      const next = !prevMuted;
      trailerIframeRef.current?.contentWindow?.postMessage(
        JSON.stringify({
          event: "command",
          func: next ? "mute" : "unMute",
          args: [],
        }),
        "*",
      );
      return next;
    });
  }, []);

  const autoplay = useRef(
    Autoplay({ delay, stopOnInteraction: false, stopOnMouseEnter: true }),
  );

  const [emblaRef, emblaApi] = useEmblaCarousel(
    { loop: true, align: "center" },
    [autoplay.current],
  );

  // ✅ backdropPath vient déjà de GetHomePageAsync (live TMDB côté backend,
  // langue/région de l'utilisateur) — plus besoin de fetch séparé ici.

  // ── Sélection ──────────────────────────────────────────────────
  const onSelect = useCallback(() => {
    if (!emblaApi) return;
    setSelectedIndex(emblaApi.selectedScrollSnap());
    // ✅ Un changement de diapo (dot, drag manuel) peut survenir pendant
    // qu'une bande-annonce joue sur une carte qui n'est plus sélectionnée —
    // on la coupe pour éviter qu'elle reste affichée sur la mauvaise carte.
    clearTrailerTimer();
    stopTrailerPlayback();
    setTrailerCardId(null);
    setTrailerKey(null);
  }, [emblaApi, clearTrailerTimer, stopTrailerPlayback]);

  useEffect(() => {
    if (!emblaApi) return;
    onSelect();
    emblaApi.on("select", onSelect);
    emblaApi.on("reInit", onSelect);
    return () => {
      emblaApi.off("select", onSelect);
      emblaApi.off("reInit", onSelect);
    };
  }, [emblaApi, onSelect]);

  const handleMouseEnter = useCallback(() => setIsPaused(true), []);
  const handleMouseLeave = useCallback(() => setIsPaused(false), []);

  if (!movies.length) return null;

  return (
    <div
      className={cn("relative w-full select-none", className)}
      onMouseEnter={handleMouseEnter}
      onMouseLeave={handleMouseLeave}
    >
      {/* ── Embla viewport ── */}
      <div className="rounded-card overflow-hidden" ref={emblaRef}>
        <div className="flex">
          {movies.map((movie, i) => {
            const backdrop = tmdbImage(movie.backdropPath, "original");
            const isSelected = selectedIndex === i;

            return (
              <div
                key={movie.id}
                className="flex-[0_0_90%] px-2 sm:flex-[0_0_70%] md:flex-[0_0_60%]"
              >
                <Link
                  href={
                    movie.contentType === "series"
                      ? `/series/${movie.id}`
                      : `/movie/${movie.id}`
                  }
                >
                  <div
                    className={cn(
                      "rounded-card h-fill relative aspect-video overflow-hidden border transition-all duration-300",
                      isSelected
                        ? "border-accent shadow-glow-lg scale-100 opacity-100"
                        : "border-border scale-95 opacity-40 hover:opacity-60",
                    )}
                  >
                    {trailerCardId === movie.id && trailerKey ? (
                      // ✅ modestbranding ne masque plus rien côté YouTube —
                      // seule technique fiable : surdimensionner l'iframe et
                      // la décaler pour pousser titre (haut) et logo (bas)
                      // hors de la zone visible, coupée par overflow-hidden
                      // du conteneur parent.
                      <div className="pointer-events-none absolute -top-[20%] left-0 h-[140%] w-full">
                        <iframe
                          ref={trailerIframeRef}
                          className="h-full w-full"
                          src={`https://www.youtube.com/embed/${trailerKey}?autoplay=1&mute=1&controls=0&loop=1&playlist=${trailerKey}&rel=0&enablejsapi=1`}
                          title={`Bande-annonce ${movie.title}`}
                          allow="autoplay; encrypted-media"
                        />
                      </div>
                    ) : backdrop ? (
                      <Image
                        src={backdrop}
                        alt={movie.title}
                        fill
                        sizes="(max-width: 640px) 90vw, (max-width: 768px) 70vw, 60vw"
                        className="object-cover transition-transform duration-500 hover:scale-105"
                      />
                    ) : (
                      // Fallback poster si pas de backdrop encore chargé
                      <div className="bg-surface-alt text-text-secondary flex h-full items-center justify-center p-4 text-center text-sm">
                        {movie.title}
                      </div>
                    )}

                    {/* Gradient bas — disparaît aussi pendant la bande-annonce */}
                    <div
                      className={cn(
                        "from-bg-primary/95 absolute inset-x-0 bottom-0 h-2/5 bg-gradient-to-t to-transparent transition-opacity duration-300",
                        trailerCardId === movie.id
                          ? "opacity-0"
                          : "opacity-100",
                      )}
                    />

                    {/* Infos sur la carte sélectionnée — disparaissent
                    pendant la lecture de la bande-annonce */}
                    {isSelected && (
                      <div
                        className={cn(
                          "absolute right-4 bottom-3 left-4 flex h-3/5 flex-col justify-end transition-opacity duration-300 sm:gap-3",
                          trailerCardId === movie.id
                            ? "pointer-events-none opacity-0"
                            : "opacity-100",
                        )}
                      >
                        <p className="font-display text-text-primary line-clamp-1 text-lg font-semibold drop-shadow md:text-xl lg:text-4xl xl:text-6xl">
                          {movie.title}
                        </p>
                        <p className="font-display text-text-primary text-sm drop-shadow md:line-clamp-3 lg:line-clamp-3 lg:w-1/2 xl:line-clamp-10">
                          {movie.overview}
                        </p>

                        <div className="text-text-secondary mt-0.5 flex items-center gap-2 text-xs">
                          {movie.genres?.slice(0, 4).map((g) => (
                            <span
                              key={g}
                              className="text-text-secondary-500 border-accent sm:py-0.5, rounded-full border-2 bg-black/40 backdrop-blur-sm sm:px-1.5 sm:text-[9px] md:px-2 md:py-1 md:text-[10px] lg:px-3 lg:py-1.5 lg:text-sm"
                            >
                              {g}
                            </span>
                          ))}
                          {releaseYear(movie.releaseDate) && (
                            <span className="text-text-primary border-accent sm:py-0.5, rounded-full border-2 font-medium backdrop-blur-sm sm:px-1.5 sm:text-[9px] md:px-2 md:py-1 md:text-[10px] lg:px-3 lg:py-1.5 lg:text-sm">
                              {releaseYear(movie.releaseDate)}
                            </span>
                          )}
                        </div>
                      </div>
                    )}

                    {/* Badge dispo */}
                    <div className="flex flex-row items-start justify-between p-2">
                      <div
                        className={cn(
                          "origin-top-left",
                          "sm:scale-100 lg:scale-200",
                        )}
                      >
                        <div className="absolute top-2 left-2">
                          <AvailabilityDot isAvailable={movie.isAvailable} />
                        </div>
                      </div>

                      <span
                        className={cn(
                          "text-text-primary drop-shadow",
                          "text-2xl font-bold",
                        )}
                      >
                        {movie.tmdbRating}
                      </span>
                    </div>

                    {/* ✅ Overlay invisible dédié au survol — au-dessus de
                    tout, indépendant du Link et de l'iframe YouTube (dont le
                    document séparé peut déclencher des mouseleave parasites).
                    Le bouton son est un ENFANT de cet overlay, pas un sibling
                    : mouseleave (contrairement à mouseover/mouseout) ne se
                    déclenche jamais en entrant sur un descendant, donc le
                    survol du bouton ne compte plus comme une sortie. */}
                    <div
                      className="absolute inset-0 z-30"
                      onMouseEnter={() => handleCardMouseEnter(movie)}
                      onMouseLeave={handleCardMouseLeave}
                    >
                      {trailerCardId === movie.id && trailerKey && (
                        <IconButton
                          variant="dark"
                          className="absolute right-2 bottom-2"
                          onClick={toggleTrailerMute}
                          aria-label={
                            trailerMuted ? "Activer le son" : "Couper le son"
                          }
                          title={
                            trailerMuted ? "Activer le son" : "Couper le son"
                          }
                        >
                          <i
                            className={cn(
                              "ph-thin text-base",
                              trailerMuted
                                ? "ph-speaker-simple-slash"
                                : "ph-speaker-simple-high",
                            )}
                          />
                        </IconButton>
                      )}
                    </div>
                  </div>
                </Link>
              </div>
            );
          })}
        </div>
      </div>

      {/* ── Dots + jauge ── */}
      <div className="mt-3 flex flex-col items-center gap-2">
        <div className="flex gap-1.5">
          {movies.map((_, i) => (
            <button
              key={i}
              onClick={() => emblaApi?.scrollTo(i)}
              className={cn(
                "rounded-full transition-all duration-200",
                selectedIndex === i
                  ? "bg-accent h-2 w-5"
                  : "bg-text-secondary/30 hover:bg-text-secondary/60 h-2 w-2",
              )}
              aria-label={`Film ${i + 1}`}
            />
          ))}
        </div>

        {/* Jauge — remonte (donc redémarre l'animation) à chaque slide */}
        <ProgressBar key={selectedIndex} duration={delay} paused={isPaused} />
      </div>
    </div>
  );
}
