import { api } from "@/lib/api";

// ── Logs (Serilog) — réservé Permission.Admin côté backend ─────────

export interface LogEntry {
  timestamp: string;
  level: string;
  sourceContext: string | null;
  message: string;
}

export interface LogsResponse {
  total: number;
  page: number;
  pageSize: number;
  results: LogEntry[];
}

export async function getLogs(params: {
  page?: number;
  pageSize?: number;
  level?: string;
  search?: string;
}): Promise<LogsResponse> {
  const query = new URLSearchParams();
  if (params.page) query.set("page", String(params.page));
  if (params.pageSize) query.set("pageSize", String(params.pageSize));
  if (params.level) query.set("level", params.level);
  if (params.search) query.set("search", params.search);
  return api.get<LogsResponse>(`/api/logs?${query.toString()}`);
}
