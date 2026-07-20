"use client";

import { useEffect, useState } from "react";
import { motion, AnimatePresence } from "framer-motion";
import {
  getRadarrQualityProfiles,
  getRadarrRootFolders,
  getSonarrQualityProfiles,
  getSonarrRootFolders,
  type ArrProfile,
  type ArrRootFolder,
} from "@/lib/auth";
import { Button } from "@/components/ui/Button";

export interface RequestOptions {
  qualityProfileId?: number;
  rootFolderPath?: string;
}

interface RequestOptionsModalProps {
  contentType: "movie" | "series";
  title: string;
  submitting: boolean;
  onConfirm: (opts: RequestOptions) => void;
  onClose: () => void;
}

// Petit modal Overseerr-like : profil qualité + dossier racine au moment de
// la demande, avant l'envoi à Radarr/Sonarr — sinon la config par défaut de
// l'admin est utilisée (voir RadarrService/SonarrService AddMovieAsync/AddSeriesAsync).
export function RequestOptionsModal({
  contentType,
  title,
  submitting,
  onConfirm,
  onClose,
}: RequestOptionsModalProps) {
  const [profiles, setProfiles] = useState<ArrProfile[]>([]);
  const [folders, setFolders] = useState<ArrRootFolder[]>([]);
  const [qualityProfileId, setQualityProfileId] = useState<number | "">("");
  const [rootFolderPath, setRootFolderPath] = useState<string>("");
  const [loading, setLoading] = useState(true);
  const [unavailable, setUnavailable] = useState(false);

  useEffect(() => {
    let cancelled = false;

    async function load() {
      setLoading(true);
      setUnavailable(false);
      try {
        const [p, f] =
          contentType === "movie"
            ? await Promise.all([
                getRadarrQualityProfiles(),
                getRadarrRootFolders(),
              ])
            : await Promise.all([
                getSonarrQualityProfiles(),
                getSonarrRootFolders(),
              ]);
        if (cancelled) return;
        setProfiles(p);
        setFolders(f);
      } catch {
        if (!cancelled) setUnavailable(true);
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    load();
    return () => {
      cancelled = true;
    };
  }, [contentType]);

  function handleConfirm() {
    onConfirm({
      qualityProfileId:
        qualityProfileId === "" ? undefined : Number(qualityProfileId),
      rootFolderPath: rootFolderPath || undefined,
    });
  }

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
          className="border-border bg-bg-primary w-full max-w-sm rounded-lg border p-5 shadow-xl"
        >
          <p className="text-text-primary mb-4 text-sm font-semibold">
            {title}
          </p>

          {loading ? (
            <div className="text-text-secondary flex items-center gap-2 py-4 text-sm">
              <div className="border-accent h-4 w-4 animate-spin rounded-full border-2 border-t-transparent" />
              Chargement des options…
            </div>
          ) : unavailable ? (
            <p className="text-text-secondary mb-4 text-xs">
              Options avancées indisponibles — la requête utilisera la
              configuration par défaut.
            </p>
          ) : (
            <div className="mb-4 space-y-3">
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

          <div className="flex justify-end gap-2">
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
              onClick={handleConfirm}
              disabled={submitting}
            >
              {submitting ? "Envoi…" : "Requêter"}
            </Button>
          </div>
        </motion.div>
      </motion.div>
    </AnimatePresence>
  );
}
