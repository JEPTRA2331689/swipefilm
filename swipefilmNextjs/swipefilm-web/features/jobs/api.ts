import { api } from "@/lib/api";

// ── Tâches planifiées (Hangfire) — réservé Permission.Admin côté backend ──

export interface JobDefinition {
  id: string;
  label: string;
  description: string;
  lastExecution: string | null;
  nextExecution: string | null;
}

export async function getJobs(): Promise<JobDefinition[]> {
  return api.get<JobDefinition[]>("/api/jobs");
}

export async function runJob(
  id: string,
): Promise<{ message: string; jobId: string; trackUrl: string }> {
  return api.post(`/api/jobs/${id}/run`);
}

export interface JobStatus {
  jobId: string;
  state: string; // Enqueued | Processing | Succeeded | Failed | Deleted | Scheduled
  createdAt: string;
}

export async function getJobStatus(jobId: string): Promise<JobStatus> {
  return api.get<JobStatus>(`/api/jobs/status/${jobId}`);
}

// ✅ Attend qu'un job Hangfire termine en pollant son état — utilisé par
// l'écran de synchronisation post-onboarding (voir SyncingScreen.tsx).
export async function waitForJob(
  jobId: string,
  { intervalMs = 1500, timeoutMs = 180_000 }: { intervalMs?: number; timeoutMs?: number } = {},
): Promise<void> {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    const { state } = await getJobStatus(jobId);
    if (state === "Succeeded") return;
    if (state === "Failed" || state === "Deleted") {
      throw new Error(`Le job a échoué (état : ${state})`);
    }
    await new Promise((r) => setTimeout(r, intervalMs));
  }
  throw new Error("Délai dépassé en attendant la fin du job.");
}
