"use client";

import { useState, useEffect, useCallback } from "react";
import { Terminal } from "lucide-react";
import { getLogs, type LogEntry } from "@/features/logs/api";
import { LogLevelBadge } from "@/components/ui/Badges";
import {
  SectionCard,
  CardBody,
  CardFooter,
  BtnSecondary,
  Spinner,
} from "@/shared/ui/FormPrimitives";

const LOG_LEVELS = [
  "Verbose",
  "Debug",
  "Information",
  "Warning",
  "Error",
  "Fatal",
];

export function LogsSection() {
  const [entries, setEntries] = useState<LogEntry[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [level, setLevel] = useState("");
  const [search, setSearch] = useState("");
  const [searchInput, setSearchInput] = useState("");
  const [loading, setLoading] = useState(true);
  const pageSize = 25;
  const totalPages = Math.max(1, Math.ceil(total / pageSize));

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await getLogs({
        page,
        pageSize,
        level: level || undefined,
        search: search || undefined,
      });
      setEntries(res.results);
      setTotal(res.total);
    } catch {
      /* silencieux */
    } finally {
      setLoading(false);
    }
  }, [page, level, search]);

  useEffect(() => {
    load();
  }, [load]);

  function handleSearchSubmit(e: React.FormEvent) {
    e.preventDefault();
    setPage(1);
    setSearch(searchInput.trim());
  }

  return (
    <SectionCard>
      <div className="border-border flex flex-wrap items-center gap-2 border-b px-6 py-4">
        <select
          value={level}
          onChange={(e) => {
            setLevel(e.target.value);
            setPage(1);
          }}
          className="rounded-input border-border bg-surface text-text-primary border px-2 py-1.5 text-sm"
        >
          <option value="">Tous les niveaux</option>
          {LOG_LEVELS.map((l) => (
            <option key={l} value={l}>
              {l}
            </option>
          ))}
        </select>

        <form
          onSubmit={handleSearchSubmit}
          className="flex min-w-[200px] flex-1 gap-2"
        >
          <input
            type="text"
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            placeholder="Rechercher dans les messages…"
            className="rounded-input border-border bg-surface text-text-primary placeholder:text-text-secondary/60 flex-1 border px-3 py-1.5 text-sm"
          />
          <BtnSecondary type="submit">Rechercher</BtnSecondary>
        </form>
      </div>

      {loading ? (
        <CardBody>
          <Spinner size="md" />
        </CardBody>
      ) : entries.length === 0 ? (
        <CardBody>
          <div className="text-text-secondary flex flex-col items-center justify-center gap-3 py-8">
            <Terminal className="size-8" />
            <p className="text-sm">Aucune entrée pour ce filtre.</p>
          </div>
        </CardBody>
      ) : (
        <ul className="divide-border divide-y">
          {entries.map((entry, i) => (
            <li key={i} className="flex items-start gap-3 px-6 py-3">
              <LogLevelBadge level={entry.level} />
              <div className="min-w-0 flex-1">
                <p className="text-text-primary text-sm break-words">
                  {entry.message}
                </p>
                <p className="text-text-secondary/70 mt-0.5 text-[11px]">
                  {new Date(entry.timestamp).toLocaleString("fr-FR")}
                  {entry.sourceContext && ` · ${entry.sourceContext}`}
                </p>
              </div>
            </li>
          ))}
        </ul>
      )}

      <CardFooter>
        <span className="text-text-secondary mr-auto text-xs">
          {total} entrée{total > 1 ? "s" : ""} · page {page}/{totalPages}
        </span>
        <BtnSecondary
          onClick={() => setPage((p) => Math.max(1, p - 1))}
          disabled={page <= 1}
        >
          Précédent
        </BtnSecondary>
        <BtnSecondary
          onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
          disabled={page >= totalPages}
        >
          Suivant
        </BtnSecondary>
      </CardFooter>
    </SectionCard>
  );
}
