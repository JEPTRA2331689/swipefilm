"use client";

import { useEffect, useMemo, useState } from "react";
import Image from "next/image";
import { motion, AnimatePresence } from "framer-motion";
import { X, Check, ChevronRight } from "lucide-react";
import { cn, tmdbImage } from "@/lib/utils";
import { type RequestStatusCode } from "@/features/catalog/api";
import {
  getSonarrQualityProfiles,
  getSonarrRootFolders,
} from "@/features/sonarr/api";
import type { ArrProfile, ArrRootFolder } from "@/shared/types/arr";
import { RequestStatusBadge } from "@/components/ui/Badges";
import { Button, IconButton } from "@/components/ui/Button";
import type { Series, SeriesSeason } from "@/types";

export interface SeriesRequestSubmitOptions {
  seasonNumbers: number[];
  qualityProfileId?: number;
  rootFolderPath?: string;
}

interface SeriesRequestModalProps {
  series: Series;
  seasons: SeriesSeason[];
  seasonsLoading: boolean;
  submitting: boolean;
  onSubmit: (opts: SeriesRequestSubmitOptions) => void;
  onClose: () => void;
}

// Modal de requête série façon Overseerr : backdrop + titre en en-tête,
// tableau de saisons (checkbox + statut), options avancées repliables,
// un seul bouton de soumission — remplace le duo grille inline + petit modal.
export function SeriesRequestModal({
  series,
  seasons,
  seasonsLoading,
  submitting,
  onSubmit,
  onClose,
}: SeriesRequestModalProps) {
  const [selected, setSelected] = useState<Set<number>>(new Set());
  const [showAdvanced, setShowAdvanced] = useState(false);

  const [profiles, setProfiles] = useState<ArrProfile[]>([]);
  const [folders, setFolders] = useState<ArrRootFolder[]>([]);
  const [qualityProfileId, setQualityProfileId] = useState<number | "">("");
  const [rootFolderPath, setRootFolderPath] = useState<string>("");
  const [optionsLoading, setOptionsLoading] = useState(true);
  const [optionsUnavailable, setOptionsUnavailable] = useState(false);

  useEffect(() => {
    let cancelled = false;
    async function load() {
      setOptionsLoading(true);
      try {
        const [p, f] = await Promise.all([
          getSonarrQualityProfiles(),
          getSonarrRootFolders(),
        ]);
        if (cancelled) return;
        setProfiles(p);
        setFolders(f);
      } catch {
        if (!cancelled) setOptionsUnavailable(true);
      } finally {
        if (!cancelled) setOptionsLoading(false);
      }
    }
    load();
    return () => {
      cancelled = true;
    };
  }, []);

  const selectableSeasons = useMemo(
    () => seasons.filter((s) => !s.isAvailable && s.requestStatus === null),
    [seasons],
  );
  const allSelected =
    selectableSeasons.length > 0 &&
    selectableSeasons.every((s) => selected.has(s.seasonNumber));

  function toggleSeason(season: SeriesSeason) {
    if (season.isAvailable || season.requestStatus !== null) return;
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(season.seasonNumber)) next.delete(season.seasonNumber);
      else next.add(season.seasonNumber);
      return next;
    });
  }

  function toggleAll() {
    setSelected(
      allSelected
        ? new Set()
        : new Set(selectableSeasons.map((s) => s.seasonNumber)),
    );
  }

  function handleSubmit() {
    onSubmit({
      seasonNumbers: Array.from(selected),
      qualityProfileId:
        qualityProfileId === "" ? undefined : Number(qualityProfileId),
      rootFolderPath: rootFolderPath || undefined,
    });
  }

  const backdrop = series.backdropPath
    ? tmdbImage(series.backdropPath, "w1280")
    : null;
  const submitLabel =
    selected.size > 0
      ? `Requêter ${selected.size} saison${selected.size > 1 ? "s" : ""}`
      : "Requêter la série";

  return (
    <AnimatePresence>
      <motion.div
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
        className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm"
        onClick={onClose}
      >
        <motion.div
          initial={{ opacity: 0, scale: 0.96 }}
          animate={{ opacity: 1, scale: 1 }}
          exit={{ opacity: 0, scale: 0.96 }}
          onClick={(e) => e.stopPropagation()}
          className="border-border bg-bg-primary w-full max-w-2xl overflow-hidden rounded-xl border shadow-xl"
        >
          {/* ── Header : backdrop + titre ── */}
          <div className="bg-surface relative h-36 w-full flex-shrink-0 overflow-hidden">
            {backdrop && (
              <Image
                src={backdrop}
                alt=""
                fill
                sizes="672px"
                className="object-cover"
                aria-hidden
              />
            )}
            <div className="from-bg-primary via-bg-primary/70 absolute inset-0 bg-gradient-to-t to-transparent" />
            <IconButton
              variant="dark"
              className="absolute top-3 right-3 bg-black/40 hover:bg-black/60"
              onClick={onClose}
            >
              <X className="size-4" />
            </IconButton>
            <div className="absolute right-14 bottom-3 left-4">
              <p className="text-text-secondary text-[11px] tracking-widest uppercase">
                Requêter
              </p>
              <p className="text-text-primary truncate text-lg font-bold">
                {series.title}
              </p>
            </div>
          </div>

          {/* ── Corps scrollable ── */}
          <div className="max-h-[55vh] overflow-y-auto">
            <div className="p-4">
              {seasonsLoading ? (
                <div className="text-text-secondary flex items-center justify-center gap-2 py-6 text-sm">
                  <div className="border-accent h-4 w-4 animate-spin rounded-full border-2 border-t-transparent" />
                  Chargement des saisons…
                </div>
              ) : seasons.length === 0 ? (
                <p className="text-text-secondary py-6 text-center text-sm">
                  Aucune saison trouvée.
                </p>
              ) : (
                <div className="border-border overflow-hidden rounded-lg border">
                  <table className="w-full text-sm">
                    <thead className="bg-surface">
                      <tr>
                        <th className="w-10 px-3 py-2">
                          {selectableSeasons.length > 0 && (
                            <button
                              onClick={toggleAll}
                              className={cn(
                                "flex h-4 w-4 items-center justify-center rounded-full border",
                                allSelected
                                  ? "border-accent bg-accent"
                                  : "border-border-strong bg-transparent",
                              )}
                              title="Tout sélectionner"
                            >
                              {allSelected && (
                                <Check className="size-2.5 text-white" />
                              )}
                            </button>
                          )}
                        </th>
                        <th className="text-text-secondary px-3 py-2 text-left text-[11px] font-semibold tracking-wider uppercase">
                          Saison
                        </th>
                        <th className="text-text-secondary px-3 py-2 text-left text-[11px] font-semibold tracking-wider uppercase">
                          Épisodes
                        </th>
                        <th className="text-text-secondary px-3 py-2 text-left text-[11px] font-semibold tracking-wider uppercase">
                          Statut
                        </th>
                      </tr>
                    </thead>
                    <tbody className="divide-border divide-y">
                      {seasons.map((season) => {
                        const selectable =
                          !season.isAvailable && season.requestStatus === null;
                        const isSelected = selected.has(season.seasonNumber);
                        const statusCode: RequestStatusCode = season.isAvailable
                          ? 4
                          : ((season.requestStatus as RequestStatusCode | null) ??
                            -1);

                        return (
                          <tr
                            key={season.seasonNumber}
                            onClick={() => toggleSeason(season)}
                            className={cn(
                              selectable
                                ? "hover:bg-surface/60 cursor-pointer"
                                : "",
                              isSelected && "bg-accent/5",
                            )}
                          >
                            <td className="px-3 py-2">
                              {selectable && (
                                <div
                                  className={cn(
                                    "flex h-4 w-4 items-center justify-center rounded-full border",
                                    isSelected
                                      ? "border-accent bg-accent"
                                      : "border-border-strong bg-transparent",
                                  )}
                                >
                                  {isSelected && (
                                    <Check className="size-2.5 text-white" />
                                  )}
                                </div>
                              )}
                            </td>
                            <td className="text-text-primary px-3 py-2 font-medium">
                              Saison {season.seasonNumber}
                            </td>
                            <td className="text-text-secondary px-3 py-2">
                              {season.episodeCount} épisode
                              {season.episodeCount > 1 ? "s" : ""}
                            </td>
                            <td className="px-3 py-2">
                              <RequestStatusBadge statusCode={statusCode} />
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              )}
            </div>

            {/* ── Options avancées ── */}
            {!optionsLoading && !optionsUnavailable && (
              <div className="px-4 pb-4">
                <button
                  onClick={() => setShowAdvanced((v) => !v)}
                  className="text-accent hover:text-accent/80 flex items-center gap-1 text-xs transition-colors"
                >
                  <ChevronRight
                    className={cn(
                      "size-4 transition-transform",
                      showAdvanced && "rotate-90",
                    )}
                  />
                  Options avancées
                </button>
                {showAdvanced && (
                  <div className="mt-3 grid grid-cols-1 gap-3 sm:grid-cols-2">
                    <div>
                      <label className="text-text-secondary mb-1 block text-[11px] tracking-wider uppercase">
                        Qualité
                      </label>
                      <select
                        value={qualityProfileId}
                        onChange={(e) =>
                          setQualityProfileId(
                            e.target.value ? Number(e.target.value) : "",
                          )
                        }
                        className="rounded-input border-border bg-surface text-text-primary w-full border px-2 py-1.5 text-sm"
                      >
                        <option value="">Par défaut</option>
                        {profiles.map((p) => (
                          <option key={p.id} value={p.id}>
                            {p.name}
                          </option>
                        ))}
                      </select>
                    </div>
                    <div>
                      <label className="text-text-secondary mb-1 block text-[11px] tracking-wider uppercase">
                        Dossier
                      </label>
                      <select
                        value={rootFolderPath}
                        onChange={(e) => setRootFolderPath(e.target.value)}
                        className="rounded-input border-border bg-surface text-text-primary w-full border px-2 py-1.5 text-sm"
                      >
                        <option value="">Par défaut</option>
                        {folders.map((f) => (
                          <option key={f.path} value={f.path}>
                            {f.path}
                          </option>
                        ))}
                      </select>
                    </div>
                  </div>
                )}
              </div>
            )}
          </div>

          {/* ── Footer ── */}
          <div className="border-border bg-surface flex justify-end gap-2 border-t px-4 py-3">
            <Button
              variant="ghost"
              size="sm"
              className="px-3 py-1.5"
              onClick={onClose}
            >
              Annuler
            </Button>
            <Button
              variant="accent-soft"
              size="sm"
              className="px-3 py-1.5"
              onClick={handleSubmit}
              disabled={submitting || seasonsLoading}
            >
              {submitting ? "Envoi…" : submitLabel}
            </Button>
          </div>
        </motion.div>
      </motion.div>
    </AnimatePresence>
  );
}
