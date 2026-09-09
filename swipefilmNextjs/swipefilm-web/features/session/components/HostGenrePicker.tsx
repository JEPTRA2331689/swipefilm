"use client";

import { useEffect, useState } from "react";
import Image from "next/image";
import { Clock, Infinity as InfinityIcon } from "lucide-react";
import { getGenreCards, type GenreCard } from "@/features/catalog/api";
import { setSessionGenre, startSession } from "@/features/session/api";
import { Button } from "@/components/ui/Button";
import { tmdbImage, cn } from "@/lib/utils";
import type { ContentTypeFilter } from "@/types";

const CONTENT_FILTERS: { value: ContentTypeFilter; label: string }[] = [
  { value: 0, label: "Films & Séries" },
  { value: 1, label: "Films" },
  { value: 2, label: "Séries" },
];

const DURATIONS: { minutes: number | null; label: string }[] = [
  { minutes: 15, label: "15 min" },
  { minutes: 30, label: "30 min" },
  { minutes: 60, label: "1 h" },
  { minutes: 120, label: "2 h" },
  { minutes: null, label: "Illimitée" },
];

// ✅ Hôte seul choisit le genre et la durée du soir — les participants ne
// votent pas, ils voient juste "l'hôte configure..." pendant ce temps (cf.
// JoinScreen côté participant, rendu par le parent selon isHost). Le QR/code
// reste affiché en permanence à côté de ce panneau (voir SessionClient) —
// pas besoin d'une étape dédiée rien que pour ça.
export function HostGenrePicker({
  code,
  onStarted,
}: {
  code: string;
  onStarted: () => void;
}) {
  const [step, setStep] = useState<"genre" | "duration">("genre");
  const [genres, setGenres] = useState<GenreCard[]>([]);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [contentType, setContentType] = useState<ContentTypeFilter>(0);
  const [loading, setLoading] = useState(true);
  const [savingGenre, setSavingGenre] = useState(false);
  const [starting, setStarting] = useState(false);

  useEffect(() => {
    getGenreCards()
      .then(setGenres)
      .catch(() => setGenres([]))
      .finally(() => setLoading(false));
  }, []);

  function toggle(genre: string) {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(genre)) next.delete(genre);
      else next.add(genre);
      return next;
    });
  }

  async function handleNext() {
    setSavingGenre(true);
    try {
      await setSessionGenre(
        code,
        selected.size > 0 ? [...selected] : null,
        contentType,
      );
      setStep("duration");
    } finally {
      setSavingGenre(false);
    }
  }

  async function handleStart(durationMinutes: number | null) {
    setStarting(true);
    try {
      await startSession(code, durationMinutes);
      onStarted();
    } finally {
      setStarting(false);
    }
  }

  if (step === "duration") {
    return (
      <div className="mx-auto w-full max-w-3xl px-4 py-8">
        <h2 className="font-display text-text-primary mb-1 text-xl font-semibold">
          Combien de temps dure la soirée ?
        </h2>
        <p className="text-text-secondary mb-5 text-sm">
          Tu peux toujours arrêter et voir les résultats plus tôt.
        </p>

        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
          {DURATIONS.map((d) => (
            <button
              key={d.label}
              type="button"
              onClick={() => handleStart(d.minutes)}
              disabled={starting}
              className="border-border bg-surface-alt hover:border-accent hover:bg-accent/10 flex flex-col items-center gap-2 rounded-lg border px-4 py-6 transition-colors disabled:opacity-40"
            >
              {d.minutes === null ? (
                <InfinityIcon className="text-accent size-5" aria-hidden />
              ) : (
                <Clock className="text-accent size-5" aria-hidden />
              )}
              <span className="text-text-primary text-sm font-medium">
                {d.label}
              </span>
            </button>
          ))}
        </div>

        {starting && (
          <p className="text-text-secondary mt-4 text-center text-xs">
            Démarrage…
          </p>
        )}
      </div>
    );
  }

  return (
    <div className="mx-auto w-full max-w-3xl px-4 py-8">
      <h2 className="font-display text-text-primary mb-1 text-xl font-semibold">
        Choisis l&apos;ambiance du soir
      </h2>
      <p className="text-text-secondary mb-5 text-sm">
        Un ou plusieurs genres, ou aucun pour un pool tout-venant.
      </p>

      <div className="mb-5 flex gap-1.5">
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

      {loading ? (
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <div
              key={i}
              className="bg-surface-alt aspect-video animate-pulse rounded-lg"
            />
          ))}
        </div>
      ) : (
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
          {genres.map((g) => {
            const isSelected = selected.has(g.genre);
            const backdrop = tmdbImage(g.backdropPath, "w500");
            return (
              <button
                key={g.genre}
                type="button"
                onClick={() => toggle(g.genre)}
                className={cn(
                  "group relative aspect-video overflow-hidden rounded-lg outline-2 outline-offset-2 outline-transparent transition-all",
                  isSelected && "outline-accent",
                )}
              >
                <div className="bg-surface-alt absolute inset-0">
                  {backdrop && (
                    <Image
                      src={backdrop}
                      alt=""
                      fill
                      sizes="280px"
                      className="object-cover"
                      aria-hidden
                    />
                  )}
                </div>
                <div
                  className={cn(
                    "absolute inset-0 bg-black/50 transition-colors",
                    isSelected && "bg-accent/40",
                  )}
                />
                <div className="absolute inset-0 flex items-center justify-center p-2">
                  <span className="font-display text-text-primary-inside-poster text-center text-sm font-semibold drop-shadow">
                    {g.genre}
                  </span>
                </div>
              </button>
            );
          })}
        </div>
      )}

      <Button
        variant="primary"
        className="mt-6 w-full"
        onClick={handleNext}
        disabled={savingGenre}
      >
        {savingGenre ? "…" : "Suivant"}
      </Button>
    </div>
  );
}
