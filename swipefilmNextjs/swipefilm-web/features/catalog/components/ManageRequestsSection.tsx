"use client";

import { useState, useEffect, useCallback } from "react";
import { CheckSquare } from "lucide-react";
import {
  getRequests,
  approveRequest,
  declineRequest,
  type MovieRequest,
} from "@/features/catalog/api";
import { MovieRequestCard } from "@/features/catalog/components/MovieRequestCard";
import {
  SectionCard,
  CardBody,
  FieldInput,
  BtnSecondary,
  BtnDanger,
  Spinner,
} from "@/shared/ui/FormPrimitives";
import { cn } from "@/lib/utils";

export function ManageRequestsSection() {
  const [requests, setRequests] = useState<MovieRequest[]>([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState<0 | null>(0); // 0=pending, null=all
  const [decliningId, setDecliningId] = useState<string | null>(null);
  const [declineReason, setDeclineReason] = useState("");
  const [actioning, setActioning] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRequests(await getRequests());
    } catch {
      /* silencieux */
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function handleApprove(id: string) {
    setActioning(id);
    try {
      await approveRequest(id);
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Erreur");
    } finally {
      setActioning(null);
    }
  }

  async function handleDeclineSubmit(id: string) {
    if (!declineReason.trim()) return;
    setActioning(id);
    try {
      await declineRequest(id, declineReason);
      setDecliningId(null);
      setDeclineReason("");
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Erreur");
    } finally {
      setActioning(null);
    }
  }

  const filtered =
    filter === 0 ? requests.filter((r) => r.status === 0) : requests;

  if (loading)
    return (
      <SectionCard>
        <CardBody>
          <Spinner size="md" />
        </CardBody>
      </SectionCard>
    );

  return (
    <div className="space-y-4">
      {/* Filtre */}
      <div className="flex gap-1.5">
        {[
          { label: "En attente", value: 0 as const },
          { label: "Toutes", value: null as null },
        ].map((f) => (
          <button
            key={String(f.value)}
            onClick={() => setFilter(f.value)}
            className={cn(
              "cursor-pointer rounded-lg border px-4 py-2 text-sm font-medium transition-colors",
              filter === f.value
                ? "bg-accent/10 border-accent/30 text-accent"
                : "border-border text-text-secondary hover:text-text-primary",
            )}
          >
            {f.label}
            {f.value === 0 &&
              requests.filter((r) => r.status === 0).length > 0 && (
                <span className="bg-accent ml-2 rounded-full px-1.5 py-0.5 text-[10px] text-white">
                  {requests.filter((r) => r.status === 0).length}
                </span>
              )}
          </button>
        ))}
      </div>

      {error && <p className="text-sm text-red-400">{error}</p>}

      {filtered.length === 0 ? (
        <SectionCard>
          <CardBody>
            <div className="text-text-secondary flex flex-col items-center justify-center gap-3 py-8">
              <CheckSquare className="size-8" />
              <p className="text-sm">
                Aucune requête{filter === 0 ? " en attente" : ""}.
              </p>
            </div>
          </CardBody>
        </SectionCard>
      ) : (
        filtered.map((req) => (
          <div key={req.id} className="space-y-2">
            <MovieRequestCard
              request={req}
              isManager
              onApprove={handleApprove}
              onDecline={(id) => {
                setDecliningId(id);
                setDeclineReason("");
              }}
            />
            {decliningId === req.id && (
              <SectionCard>
                <CardBody className="py-4">
                  <p className="text-text-primary mb-3 text-sm font-medium">
                    Raison du refus
                  </p>
                  <div className="flex gap-2">
                    <FieldInput
                      value={declineReason}
                      onChange={(e) => setDeclineReason(e.target.value)}
                      placeholder="Ex : déjà disponible, hors catalogue…"
                      className="flex-1"
                    />
                    <BtnDanger
                      onClick={() => handleDeclineSubmit(req.id)}
                      disabled={!declineReason.trim() || actioning === req.id}
                    >
                      {actioning === req.id ? <Spinner /> : "Refuser"}
                    </BtnDanger>
                    <BtnSecondary onClick={() => setDecliningId(null)}>
                      Annuler
                    </BtnSecondary>
                  </div>
                </CardBody>
              </SectionCard>
            )}
          </div>
        ))
      )}
    </div>
  );
}
