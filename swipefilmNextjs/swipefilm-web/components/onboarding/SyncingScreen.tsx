"use client";

import { useEffect, useRef, useState } from "react";
import { CircleCheck, AlertTriangle } from "lucide-react";
import { triggerSync, discoverRecommendations } from "@/features/media-server/api";
import { waitForJob } from "@/features/jobs/api";

type Phase = "sync" | "discover" | "done" | "error";

const PHASE_LABEL: Record<Phase, string> = {
  sync: "Récupération de vos films et séries…",
  discover: "Construction de vos recommandations…",
  done: "Prêt !",
  error: "Une erreur est survenue",
};

interface SyncingScreenProps {
  onDone: () => void;
}

// ✅ La sync (Jellyfin/Plex → catalogue local) et la découverte
// personnalisée (recommandations) tournent en tâche de fond côté backend —
// cet écran attend les deux avant de laisser entrer l'utilisateur, plutôt
// que de le lâcher sur un /home vide le temps que ça se termine.
export function SyncingScreen({ onDone }: SyncingScreenProps) {
  const [phase, setPhase] = useState<Phase>("sync");
  const [error, setError] = useState<string | null>(null);
  const started = useRef(false);

  useEffect(() => {
    if (started.current) return;
    started.current = true;

    async function run() {
      try {
        setPhase("sync");
        const { jobId } = await triggerSync();
        await waitForJob(jobId, { timeoutMs: 5 * 60_000 });

        setPhase("discover");
        await discoverRecommendations();

        setPhase("done");
        setTimeout(onDone, 600);
      } catch (e) {
        setPhase("error");
        setError(e instanceof Error ? e.message : "Erreur inconnue");
      }
    }

    run();
  }, [onDone]);

  return (
    <div className="mx-auto flex w-full max-w-md flex-col items-center gap-6 py-8 text-center">
      {phase === "error" ? (
        <AlertTriangle className="text-swipe-skip size-10" />
      ) : phase === "done" ? (
        <CircleCheck className="text-success size-10" />
      ) : (
        <div className="border-accent h-10 w-10 animate-spin rounded-full border-2 border-t-transparent" />
      )}

      <div>
        <h2 className="font-display text-text-primary text-xl font-semibold">
          {phase === "error" ? "Une erreur est survenue" : "Presque prêt…"}
        </h2>
        <p className="text-text-secondary mt-1.5 text-sm">
          {phase === "error" ? error : PHASE_LABEL[phase]}
        </p>
      </div>

      {/* Étapes */}
      <div className="flex w-full flex-col gap-2 text-left text-sm">
        <SyncStep
          label="Films et séries"
          active={phase === "sync"}
          done={phase === "discover" || phase === "done"}
          failed={phase === "error"}
        />
        <SyncStep
          label="Recommandations personnalisées"
          active={phase === "discover"}
          done={phase === "done"}
          failed={false}
        />
      </div>
    </div>
  );
}

function SyncStep({
  label,
  active,
  done,
  failed,
}: {
  label: string;
  active: boolean;
  done: boolean;
  failed: boolean;
}) {
  return (
    <div className="border-border bg-surface flex items-center gap-3 border px-4 py-3">
      {done ? (
        <CircleCheck className="text-success size-4 flex-shrink-0" />
      ) : failed ? (
        <AlertTriangle className="text-swipe-skip size-4 flex-shrink-0" />
      ) : active ? (
        <span className="border-accent h-4 w-4 flex-shrink-0 animate-spin rounded-full border-2 border-t-transparent" />
      ) : (
        <span className="border-border-strong h-4 w-4 flex-shrink-0 rounded-full border-2" />
      )}
      <span
        className={
          active || done ? "text-text-primary" : "text-text-secondary"
        }
      >
        {label}
      </span>
    </div>
  );
}
