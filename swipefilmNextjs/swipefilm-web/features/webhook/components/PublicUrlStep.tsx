"use client";

import { useState } from "react";
import { Globe } from "lucide-react";

// ✅ Toujours renseignée par l'admin — jamais devinée depuis l'URL du
// navigateur. Radarr/Sonarr doivent pouvoir joindre cette adresse pour
// appeler le webhook de SwipeFilm ; localhost ou le mauvais port cassent
// l'enregistrement (voir onVerifyWebhook dans ArrOnboardingStep).

export function PublicUrlStep({
  onSubmit,
  onSkip,
}: {
  onSubmit: (url: string) => Promise<void>;
  onSkip: () => Promise<void>;
}) {
  const [url, setUrl] = useState(() => {
    if (typeof window === "undefined") return "";
    return window.location.origin.replace(/^https:\/\//i, "http://");
  });
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!url) {
      setError("Adresse requise (ou passe cette étape).");
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      await onSubmit(url);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Erreur");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="mx-auto w-full max-w-md">
      <div className="mb-1 flex items-center gap-3">
        <div className="bg-accent/10 border-accent/20 flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-full border">
          <Globe className="size-5 text-accent" aria-hidden />
        </div>
        <h2 className="font-display text-text-primary text-xl font-bold">
          Adresse publique
        </h2>
      </div>
      <p className="text-text-secondary mb-6 ml-12 text-sm">
        L&apos;adresse que Radarr et Sonarr utiliseront pour contacter
        SwipeFilm (webhooks de téléchargement). Doit être joignable depuis
        leurs serveurs — pas <code>localhost</code>.
      </p>

      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label className="text-text-secondary mb-1.5 block text-xs font-medium tracking-wider uppercase">
            URL
          </label>
          <input
            type="url"
            value={url}
            onChange={(e) => setUrl(e.target.value)}
            placeholder="http://192.168.1.10:5250"
            className="rounded-button border-border bg-surface text-text-primary placeholder:text-text-secondary/50 focus:border-accent/60 w-full border px-3 py-2.5 text-sm transition-colors focus:outline-none"
          />
        </div>

        {error && <p className="text-swipe-skip text-xs">{error}</p>}

        <button
          type="submit"
          disabled={submitting || !url}
          className="rounded-button bg-accent hover:bg-accent/90 flex w-full items-center justify-center gap-2 px-4 py-2.5 text-sm font-medium text-white transition-all disabled:cursor-not-allowed disabled:opacity-40"
        >
          {submitting ? (
            <span className="h-3.5 w-3.5 animate-spin rounded-full border-2 border-white border-t-transparent" />
          ) : (
            "Continuer"
          )}
        </button>

        <button
          type="button"
          onClick={onSkip}
          className="text-text-secondary/60 hover:text-text-secondary w-full py-1 text-xs transition-colors"
        >
          Passer cette étape →
        </button>
      </form>
    </div>
  );
}
