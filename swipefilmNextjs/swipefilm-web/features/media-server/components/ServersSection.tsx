"use client";

import { useState, useEffect, useCallback } from "react";
import { Radio, Pencil, RefreshCw } from "lucide-react";
import { ConnectForm } from "@/components/onboarding/ConnectForm";
import {
  getServerConfig,
  configureServer,
  syncServer,
  normalizeServer,
} from "@/features/media-server/api";
import {
  SectionCard,
  CardBody,
  BtnSecondary,
  Spinner,
} from "@/shared/ui/FormPrimitives";
import type { UserServer } from "@/types";

export function ServersSection() {
  const [server, setServer] = useState<UserServer | null>(null);
  const [loading, setLoading] = useState(true);
  const [syncing, setSyncing] = useState(false);
  const [showForm, setShowForm] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const s = await getServerConfig();
      setServer(s ? normalizeServer(s) : null);
    } catch {
      /* silencieux */
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function handleSync() {
    setSyncing(true);
    setError(null);
    try {
      await syncServer();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Erreur");
    } finally {
      setSyncing(false);
    }
  }

  async function handleConfigure(data: {
    type: string;
    friendlyName: string;
    url: string;
    apiKey?: string;
    username?: string;
    password?: string;
  }) {
    await configureServer({
      friendlyName: data.friendlyName,
      type: data.type === "jellyfin" ? "jellyfin" : "plex",
      apiKey: data.apiKey,
      username: data.username,
      password: data.password,
      url: data.url,
    });
    setShowForm(false);
    await load();
  }

  if (loading)
    return (
      <SectionCard>
        <CardBody>
          <Spinner size="md" />
        </CardBody>
      </SectionCard>
    );

  // ✅ Un seul serveur pour toute l'instance — formulaire de (re)configuration
  // à la place de la liste + bouton "ajouter" d'avant.
  if (!server || showForm) {
    return (
      <SectionCard>
        <CardBody>
          <ConnectForm
            title={
              server ? "Reconfigurer le serveur" : "Connecter un serveur média"
            }
            options={[
              { id: "jellyfin", label: "Jellyfin" },
              { id: "plex", label: "Plex" },
            ]}
            onSubmit={handleConfigure}
            onBack={server ? () => setShowForm(false) : undefined}
          />
        </CardBody>
      </SectionCard>
    );
  }

  const typeLabel =
    server.type === 1 || server.type === "jellyfin" ? "Jellyfin" : "Plex";

  return (
    <SectionCard>
      {error && (
        <div className="px-6 pt-4">
          <p className="text-sm text-red-400">{error}</p>
        </div>
      )}
      <div className="flex items-center gap-4 px-6 py-4">
        <div className="bg-accent/10 border-accent/20 flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-lg border">
          <Radio className="size-4 text-accent" />
        </div>
        <div className="min-w-0 flex-1">
          <p className="text-text-primary truncate text-sm font-semibold">
            {server.friendlyName ?? typeLabel}
          </p>
          <p className="text-text-secondary text-xs">
            {typeLabel}
            {server.lastSyncAt &&
              ` · Sync ${new Date(server.lastSyncAt).toLocaleString()}`}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <BtnSecondary onClick={handleSync} disabled={syncing}>
            {syncing ? (
              <Spinner />
            ) : (
              <RefreshCw className="size-4" />
            )}
            Sync
          </BtnSecondary>
          <BtnSecondary onClick={() => setShowForm(true)}>
            <Pencil className="size-4" />
            Reconfigurer
          </BtnSecondary>
        </div>
      </div>
    </SectionCard>
  );
}
