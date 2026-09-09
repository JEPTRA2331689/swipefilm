"use client";

import { useState, useEffect, useCallback } from "react";
import { CircleCheck, CircleAlert, RefreshCw, type LucideIcon } from "lucide-react";
import { cn } from "@/lib/utils";
import type {
  ArrConfig,
  ArrPreferences,
  ArrProfile,
  ArrRootFolder,
  WebhookVerifyResult,
} from "@/shared/types/arr";
import {
  SectionCard,
  CardBody,
  CardFooter,
  FieldLabel,
  FieldInput,
  FieldSelect,
  BtnPrimary,
  BtnSecondary,
  Spinner,
  type SaveState,
  type VerifyState,
} from "@/shared/ui/FormPrimitives";

// ✅ Composant générique paramétré par service — utilisé par
// features/radarr/components/RadarrSettingsSection.tsx et
// features/sonarr/components/SonarrSettingsSection.tsx (mêmes flux
// test/save/preferences/webhook côté backend pour les deux).

interface ArrConnectionSectionProps {
  label: string;
  description: string;
  icon: LucideIcon;
  urlPlaceholder: string;
  onGet: () => Promise<ArrConfig | null>;
  onUpdate: (
    url: string,
    apiKey: string,
    prefs?: Partial<ArrPreferences>,
  ) => Promise<{ message: string; testConnectionOk: boolean }>;
  onGetProfiles: () => Promise<ArrProfile[]>;
  onGetFolders: () => Promise<ArrRootFolder[]>;
  onVerifyWebhook: () => Promise<WebhookVerifyResult>;
}

export function ArrConnectionSection({
  label,
  description,
  icon: Icon,
  urlPlaceholder,
  onGet,
  onUpdate,
  onGetProfiles,
  onGetFolders,
  onVerifyWebhook,
}: ArrConnectionSectionProps) {
  const [config, setConfig] = useState<ArrConfig | null>(null);
  const [loading, setLoading] = useState(true);
  const [url, setUrl] = useState("");
  const [apiKey, setApiKey] = useState("");
  const [saveState, setSaveState] = useState<SaveState>("idle");
  const [saveError, setSaveError] = useState<string | null>(null);
  const [profiles, setProfiles] = useState<ArrProfile[]>([]);
  const [folders, setFolders] = useState<ArrRootFolder[]>([]);
  const [loadingPrefs, setLoadingPrefs] = useState(false);
  const [selectedProfileId, setSelectedProfileId] = useState<number | null>(
    null,
  );
  const [selectedFolder, setSelectedFolder] = useState("");
  const [verifyState, setVerifyState] = useState<VerifyState>("idle");
  const [verifyResult, setVerifyResult] = useState<WebhookVerifyResult | null>(
    null,
  );

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const cfg = await onGet();
      setConfig(cfg);
      if (cfg) {
        setUrl(cfg.url ?? "");
        // Ne pas pré-remplir la clé — on affiche seulement le hint
        if (cfg.isConfigured) {
          setSelectedProfileId(cfg.defaultQualityProfileId ?? null);
          setSelectedFolder(cfg.defaultRootFolderPath ?? "");
          // Charger les profils / dossiers si déjà configuré
          setLoadingPrefs(true);
          Promise.all([onGetProfiles(), onGetFolders()])
            .then(([p, f]) => {
              setProfiles(p);
              setFolders(f);
              if (p.length > 0 && !cfg.defaultQualityProfileId)
                setSelectedProfileId(p[0].id);
              if (f.length > 0 && !cfg.defaultRootFolderPath)
                setSelectedFolder(f[0].path);
            })
            .finally(() => setLoadingPrefs(false));
        }
      }
    } finally {
      setLoading(false);
    }
  }, [onGet, onGetProfiles, onGetFolders]);

  useEffect(() => {
    load();
  }, [load]);

  async function handleSave() {
    if (!url || !apiKey) {
      setSaveError("URL et clé API requis.");
      return;
    }
    setSaveState("saving");
    setSaveError(null);
    try {
      const prefs: Partial<ArrPreferences> = {};
      if (selectedProfileId !== null)
        prefs.defaultQualityProfileId = selectedProfileId;
      if (selectedFolder) prefs.defaultRootFolderPath = selectedFolder;
      const res = await onUpdate(url, apiKey, prefs);
      if (!res.testConnectionOk)
        throw new Error("Connexion échouée — vérifiez l'URL et la clé API");
      setSaveState("ok");
      // Recharger les profils / dossiers avec la nouvelle config
      setLoadingPrefs(true);
      const [p, f] = await Promise.all([onGetProfiles(), onGetFolders()]);
      setProfiles(p);
      setFolders(f);
      if (p.length > 0 && selectedProfileId === null)
        setSelectedProfileId(p[0].id);
      if (f.length > 0 && !selectedFolder) setSelectedFolder(f[0].path);
      setLoadingPrefs(false);
      await load();
    } catch (e) {
      setSaveState("error");
      setSaveError(e instanceof Error ? e.message : "Erreur de connexion");
    }
  }

  async function handleVerifyWebhook() {
    setVerifyState("verifying");
    setVerifyResult(null);
    try {
      const result = await onVerifyWebhook();
      setVerifyResult(result);
      setVerifyState(result.success ? "ok" : "error");
    } catch (e) {
      setVerifyResult({
        success: false,
        callbackUrl: "",
        error: e instanceof Error ? e.message : "Erreur inconnue",
      });
      setVerifyState("error");
    }
  }

  const isConfigured = config?.isConfigured ?? false;
  const showPrefs = isConfigured || saveState === "ok";

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
      {/* Status header */}
      <div className="border-border flex items-center gap-4 border-b px-6 py-5">
        <div
          className={cn(
            "flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-lg",
            isConfigured ? "bg-accent/10" : "bg-surface-alt",
          )}
        >
          <Icon
            className={cn(
              "size-5",
              isConfigured ? "text-accent" : "text-text-secondary",
            )}
          />
        </div>
        <div className="flex-1">
          <div className="flex items-center gap-2">
            <span className="text-text-primary font-semibold">{label}</span>
            <span
              className={cn(
                "rounded-full px-2 py-0.5 text-[10px] font-medium tracking-wider uppercase",
                isConfigured
                  ? "bg-green-500/15 text-green-400"
                  : "bg-surface-alt text-text-secondary/60",
              )}
            >
              {isConfigured ? "Connecté" : "Non configuré"}
            </span>
          </div>
          <p className="text-text-secondary mt-0.5 text-xs">{description}</p>
        </div>
      </div>

      <CardBody className="space-y-5">
        <div>
          <FieldLabel>URL du serveur</FieldLabel>
          <FieldInput
            type="url"
            value={url}
            onChange={(e) => {
              setUrl(e.target.value);
              setSaveState("idle");
            }}
            placeholder={urlPlaceholder}
          />
        </div>
        <div>
          <FieldLabel>
            Clé API{" "}
            {isConfigured && config?.apiKeyHint && (
              <span className="text-text-secondary/50 font-normal">
                (actuelle : {config.apiKeyHint}…)
              </span>
            )}
          </FieldLabel>
          <FieldInput
            value={apiKey}
            onChange={(e) => {
              setApiKey(e.target.value);
              setSaveState("idle");
            }}
            placeholder={
              isConfigured
                ? "Laisser vide pour conserver l'actuelle"
                : "••••••••••••••••••••••••••••••••"
            }
            className="font-mono"
          />
        </div>

        {showPrefs && (
          <div className="border-border bg-bg-primary/50 space-y-4 rounded-lg border p-4">
            <p className="text-text-secondary text-xs font-semibold tracking-wider uppercase">
              Préférences de téléchargement
            </p>
            {loadingPrefs ? (
              <div className="text-text-secondary flex items-center gap-2 text-xs">
                <Spinner />
                Chargement…
              </div>
            ) : (
              <>
                {profiles.length > 0 && (
                  <div>
                    <FieldLabel>Profil de qualité</FieldLabel>
                    <FieldSelect
                      value={selectedProfileId ?? ""}
                      onChange={(e) =>
                        setSelectedProfileId(Number(e.target.value))
                      }
                    >
                      {profiles.map((p) => (
                        <option key={p.id} value={p.id}>
                          {p.name}
                        </option>
                      ))}
                    </FieldSelect>
                  </div>
                )}
                {folders.length > 0 && (
                  <div>
                    <FieldLabel>Dossier de destination</FieldLabel>
                    <FieldSelect
                      value={selectedFolder}
                      onChange={(e) => setSelectedFolder(e.target.value)}
                    >
                      {folders.map((f) => (
                        <option key={f.path} value={f.path}>
                          {f.path}
                          {f.freeSpaceGb !== undefined
                            ? ` (${f.freeSpaceGb.toFixed(1)} Go libres)`
                            : ""}
                        </option>
                      ))}
                    </FieldSelect>
                  </div>
                )}
              </>
            )}
          </div>
        )}

        {saveState === "error" && saveError && (
          <p className="text-sm text-red-400">{saveError}</p>
        )}
        {saveState === "ok" && (
          <p className="flex items-center gap-1.5 text-sm text-green-400">
            <CircleCheck className="size-4" />
            Configuration enregistrée et connexion vérifiée.
          </p>
        )}

        {isConfigured && (
          <div className="border-border bg-bg-primary/50 space-y-2 rounded-lg border p-4">
            <p className="text-text-secondary text-xs font-semibold tracking-wider uppercase">
              Webhook de notification
            </p>
            <p className="text-text-secondary text-xs">
              {label} notifie SwipeFilm dès qu'un téléchargement est
              importé. Utilise ce bouton si les notifications ne semblent
              pas arriver (ex: connexion supprimée manuellement côté{" "}
              {label}).
            </p>
            <BtnSecondary
              onClick={handleVerifyWebhook}
              disabled={verifyState === "verifying"}
            >
              {verifyState === "verifying" ? (
                <>
                  <Spinner />
                  Vérification…
                </>
              ) : (
                <>
                  <RefreshCw className="size-4" />
                  Vérifier le webhook
                </>
              )}
            </BtnSecondary>
            {verifyResult && (
              <p
                className={cn(
                  "flex items-start gap-1.5 text-sm",
                  verifyResult.success ? "text-green-400" : "text-red-400",
                )}
              >
                {verifyResult.success ? (
                  <CircleCheck className="mt-0.5 size-4 flex-shrink-0" />
                ) : (
                  <CircleAlert className="mt-0.5 size-4 flex-shrink-0" />
                )}
                <span>
                  {verifyResult.success
                    ? `Webhook enregistré (${verifyResult.callbackUrl})`
                    : (verifyResult.error ?? "Échec de la vérification")}
                </span>
              </p>
            )}
          </div>
        )}
      </CardBody>

      <CardFooter>
        <BtnPrimary
          onClick={handleSave}
          disabled={saveState === "saving" || !url}
        >
          {saveState === "saving" ? (
            <>
              <Spinner />
              Connexion en cours…
            </>
          ) : (
            "Enregistrer"
          )}
        </BtnPrimary>
      </CardFooter>
    </SectionCard>
  );
}
