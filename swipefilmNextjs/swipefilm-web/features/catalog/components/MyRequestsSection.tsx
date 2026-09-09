"use client";

import { useState, useEffect, useCallback } from "react";
import { Layers } from "lucide-react";
import { getRequests, cancelRequest, type MovieRequest } from "@/features/catalog/api";
import { MovieRequestCard } from "@/features/catalog/components/MovieRequestCard";
import { SectionCard, CardBody, Spinner } from "@/shared/ui/FormPrimitives";

export function MyRequestsSection() {
  const [requests, setRequests] = useState<MovieRequest[]>([]);
  const [loading, setLoading] = useState(true);
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

  async function handleCancel(id: string) {
    try {
      await cancelRequest(id);
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Erreur");
    }
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
    <div className="space-y-4">
      {error && <p className="text-sm text-red-400">{error}</p>}
      {requests.length === 0 ? (
        <SectionCard>
          <CardBody>
            <div className="text-text-secondary flex flex-col items-center justify-center gap-3 py-8">
              <Layers className="size-8" />
              <p className="text-sm">Aucune requête pour l&apos;instant.</p>
            </div>
          </CardBody>
        </SectionCard>
      ) : (
        requests.map((req) => (
          <MovieRequestCard
            key={req.id}
            request={req}
            onCancel={handleCancel}
          />
        ))
      )}
    </div>
  );
}
