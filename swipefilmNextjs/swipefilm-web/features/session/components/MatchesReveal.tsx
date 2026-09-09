"use client";

import { useState } from "react";
import { Users, PlayCircle } from "lucide-react";
import { RequestOptionsModal, type RequestOptions } from "@/features/catalog/components/RequestOptionsModal";
import { createRequest } from "@/features/catalog/api";
import { ButtonLink } from "@/components/ui/Button";
import { tmdbImage } from "@/lib/utils";
import type { SessionMatchResult } from "@/features/session/api";

export function MatchesReveal({ matches }: { matches: SessionMatchResult[] }) {
  const [target, setTarget] = useState<SessionMatchResult | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [requested, setRequested] = useState<Set<string>>(new Set());

  async function handleConfirm(opts: RequestOptions) {
    if (!target) return;
    setSubmitting(true);
    try {
      await createRequest({
        movieId: target.movieId ?? undefined,
        seriesId: target.seriesId ?? undefined,
        ...opts,
      });
      setRequested((prev) => new Set(prev).add(target.movieId ?? target.seriesId ?? ""));
      setTarget(null);
    } finally {
      setSubmitting(false);
    }
  }

  if (matches.length === 0) {
    return (
      <div className="text-text-secondary flex flex-1 flex-col items-center justify-center gap-2 py-16 text-center text-sm">
        <Users className="size-8 opacity-40" aria-hidden />
        Personne n&apos;a trouvé de consensus cette fois — réessayez avec un
        autre genre !
      </div>
    );
  }

  return (
    <div className="mx-auto w-full max-w-4xl px-4 py-8">
      <h2 className="font-display text-text-primary mb-1 text-xl font-semibold">
        Les résultats du groupe
      </h2>
      <p className="text-text-secondary mb-6 text-sm">
        Classés par consensus — le pourcentage du groupe qui a aimé.
      </p>

      <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-4">
        {matches.map((m) => {
          const key = m.movieId ?? m.seriesId ?? m.title;
          const poster = tmdbImage(m.posterPath, "w342");
          return (
            <div key={key} className="flex flex-col gap-2">
              <div className="bg-surface-alt relative aspect-[2/3] overflow-hidden rounded-lg">
                {poster ? (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img src={poster} alt={m.title} className="h-full w-full object-cover" />
                ) : (
                  <div className="text-text-secondary flex h-full items-center justify-center p-2 text-center text-xs">
                    {m.title}
                  </div>
                )}
                <div className="absolute top-2 right-2 rounded-full bg-black/70 px-2 py-0.5 text-xs font-semibold text-white">
                  {Math.round(m.agreePct * 100)}%
                </div>
              </div>
              <p className="text-text-primary line-clamp-1 text-sm font-medium">{m.title}</p>
              <p className="text-text-secondary text-xs">
                {m.agreeCount}/{m.memberCount} ont aimé
              </p>
              {/* ✅ Même schéma que la fiche film/série (MovieDetailClient) :
              déjà dispo → lien direct Jellyfin/Plex, sinon Requêter. */}
              {m.isAvailable && (m.jellyfinUrl || m.plexUrl) ? (
                <ButtonLink
                  variant="secondary"
                  href={(m.jellyfinUrl ?? m.plexUrl)!}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="border-swipe-star/40 bg-swipe-star/10 text-swipe-star hover:bg-swipe-star/20 gap-1.5 rounded-md border px-2 py-1.5 text-xs font-medium"
                >
                  <PlayCircle className="size-3.5" aria-hidden />
                  {m.jellyfinUrl ? "Ouvrir dans Jellyfin" : "Ouvrir dans Plex"}
                </ButtonLink>
              ) : (
                <button
                  type="button"
                  onClick={() => setTarget(m)}
                  disabled={requested.has(key)}
                  className="border-accent/40 bg-accent/10 text-accent hover:bg-accent/20 rounded-md border px-2 py-1.5 text-xs font-medium disabled:opacity-40"
                >
                  {requested.has(key) ? "Requêtée ✓" : "Requêter"}
                </button>
              )}
            </div>
          );
        })}
      </div>

      {target && (
        <RequestOptionsModal
          contentType={target.movieId ? "movie" : "series"}
          title={target.title}
          submitting={submitting}
          onConfirm={handleConfirm}
          onClose={() => setTarget(null)}
        />
      )}
    </div>
  );
}
