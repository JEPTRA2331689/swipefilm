"use client";

import { useState, useEffect, useCallback } from "react";
import { Settings2, Play } from "lucide-react";
import { getJobs, runJob, type JobDefinition } from "@/features/jobs/api";
import {
  SectionCard,
  CardBody,
  BtnSecondary,
  Spinner,
} from "@/shared/ui/FormPrimitives";

export function JobsSection() {
  const [jobs, setJobs] = useState<JobDefinition[]>([]);
  const [loading, setLoading] = useState(true);
  const [running, setRunning] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<Record<string, string>>({});

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setJobs(await getJobs());
    } catch {
      /* silencieux */
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function handleRun(id: string) {
    setRunning(id);
    setFeedback((f) => ({ ...f, [id]: "" }));
    try {
      await runJob(id);
      setFeedback((f) => ({ ...f, [id]: "Job lancé ⏳" }));
    } catch (e) {
      setFeedback((f) => ({
        ...f,
        [id]: e instanceof Error ? e.message : "Erreur",
      }));
    } finally {
      setRunning(null);
    }
  }

  function formatDate(iso: string | null) {
    if (!iso) return "jamais";
    return new Date(iso).toLocaleString("fr-FR", {
      dateStyle: "short",
      timeStyle: "short",
    });
  }

  if (loading)
    return (
      <SectionCard>
        <CardBody>
          <Spinner size="md" />
        </CardBody>
      </SectionCard>
    );

  return (
    <SectionCard>
      {jobs.length === 0 ? (
        <CardBody>
          <div className="text-text-secondary flex flex-col items-center justify-center gap-3 py-8">
            <Settings2 className="size-8" />
            <p className="text-sm">Aucun job enregistré.</p>
          </div>
        </CardBody>
      ) : (
        <ul className="divide-border divide-y">
          {jobs.map((job) => (
            <li key={job.id} className="flex items-center gap-4 px-6 py-4">
              <div className="bg-accent/10 border-accent/20 flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-lg border">
                <Settings2 className="size-4 text-accent" />
              </div>
              <div className="min-w-0 flex-1">
                <p className="text-text-primary truncate text-sm font-semibold">
                  {job.label}
                </p>
                <p className="text-text-secondary text-xs">{job.description}</p>
                <p className="text-text-secondary/70 mt-1 text-[11px]">
                  Dernière exécution : {formatDate(job.lastExecution)} ·
                  Prochaine : {formatDate(job.nextExecution)}
                </p>
                {feedback[job.id] && (
                  <p className="text-accent mt-1 text-xs">{feedback[job.id]}</p>
                )}
              </div>
              <BtnSecondary
                onClick={() => handleRun(job.id)}
                disabled={running === job.id}
              >
                {running === job.id ? (
                  <Spinner />
                ) : (
                  <Play className="size-4" />
                )}
                Lancer
              </BtnSecondary>
            </li>
          ))}
        </ul>
      )}
    </SectionCard>
  );
}
