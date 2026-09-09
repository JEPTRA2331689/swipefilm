"use client";

import { useState } from "react";
import {
  CircleCheck,
  AlertTriangle,
  Plug,
  RefreshCw,
  type LucideIcon,
} from "lucide-react";
import { cn } from "@/lib/utils";
import type { WebhookVerifyResult } from "@/shared/types/arr";

// ✅ Composant générique paramétré par service — utilisé par
// features/radarr/components/RadarrOnboardingStep.tsx et
// features/sonarr/components/SonarrOnboardingStep.tsx pendant l'assistant
// de configuration initiale. Sans carte propre — vit à l'intérieur de la
// carte de contenu partagée de app/onboarding/page.tsx.

interface ArrOnboardingStepProps {
  title: string;
  subtitle: string;
  icon: LucideIcon;
  onTest: (url: string, apiKey: string) => Promise<void>;
  onSubmit: (data: { url: string; apiKey: string }) => Promise<void>;
  onVerifyWebhook: () => Promise<WebhookVerifyResult>;
  onContinue: () => void;
  onSkip: () => Promise<void>;
}

export function ArrOnboardingStep({
  title,
  subtitle,
  icon: Icon,
  onTest,
  onSubmit,
  onVerifyWebhook,
  onContinue,
  onSkip,
}: ArrOnboardingStepProps) {
  const [url, setUrl] = useState("");
  const [apiKey, setApiKey] = useState("");
  const [testState, setTestState] = useState<
    "idle" | "testing" | "ok" | "error"
  >("idle");
  const [testError, setTestError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [verifyState, setVerifyState] = useState<
    "idle" | "verifying" | "ok" | "error"
  >("idle");
  const [verifyResult, setVerifyResult] = useState<WebhookVerifyResult | null>(
    null,
  );

  async function handleTest() {
    if (!url || !apiKey) return;
    setTestState("testing");
    setTestError(null);
    try {
      await onTest(url, apiKey);
      setTestState("ok");
    } catch (e) {
      setTestState("error");
      setTestError(e instanceof Error ? e.message : "Connexion échouée");
    }
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!url || !apiKey) {
      setError("URL et clé API requis.");
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      await onSubmit({ url, apiKey });
      setSaved(true);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Erreur");
    } finally {
      setSubmitting(false);
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

  if (saved) {
    return (
      <div className="mx-auto w-full max-w-md">
        <div className="mb-1 flex items-center gap-3">
          <div className="bg-success/10 border-success/20 flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-full border">
            <CircleCheck className="text-success size-5" aria-hidden />
          </div>
          <h2 className="font-display text-text-primary text-xl font-bold">
            {title} — connecté
          </h2>
        </div>
        <p className="text-text-secondary mb-6 ml-12 text-sm">
          {title.replace("Configurer ", "")} notifiera SwipeFilm dès qu'un
          téléchargement est importé.
        </p>

        <div className="border-border bg-surface mb-4 space-y-3 rounded-lg border p-4">
          <p className="text-text-secondary text-xs font-semibold tracking-wider uppercase">
            Webhook de notification
          </p>
          <button
            type="button"
            onClick={handleVerifyWebhook}
            disabled={verifyState === "verifying"}
            className="rounded-button border-border bg-bg-primary text-text-secondary hover:text-text-primary hover:border-accent/40 flex w-full items-center justify-center gap-2 border px-4 py-2.5 text-sm font-medium transition-all disabled:cursor-not-allowed disabled:opacity-40"
          >
            {verifyState === "verifying" ? (
              <>
                <span className="h-3.5 w-3.5 animate-spin rounded-full border-2 border-current border-t-transparent" />
                Vérification…
              </>
            ) : (
              <>
                <RefreshCw className="size-4" /> Vérifier le webhook
              </>
            )}
          </button>
          {verifyResult && (
            <p
              className={cn(
                "flex items-start gap-1.5 text-xs",
                verifyResult.success ? "text-success" : "text-swipe-skip",
              )}
            >
              {verifyResult.success ? (
                <CircleCheck className="mt-0.5 size-3.5 flex-shrink-0" />
              ) : (
                <AlertTriangle className="mt-0.5 size-3.5 flex-shrink-0" />
              )}
              <span>
                {verifyResult.success
                  ? `Webhook enregistré (${verifyResult.callbackUrl})`
                  : (verifyResult.error ?? "Échec de la vérification")}
              </span>
            </p>
          )}
        </div>

        <button
          type="button"
          onClick={onContinue}
          className="rounded-button bg-accent hover:bg-accent/90 flex w-full items-center justify-center gap-2 px-4 py-2.5 text-sm font-medium text-white transition-all"
        >
          Continuer
        </button>
      </div>
    );
  }

  return (
    <div className="mx-auto w-full max-w-md">
      {/* Header */}
      <div className="mb-1 flex items-center gap-3">
        <div className="bg-accent/10 border-accent/20 flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-full border">
          <Icon className="size-5 text-accent" aria-hidden />
        </div>
        <h2 className="font-display text-text-primary text-xl font-bold">
          {title}
        </h2>
      </div>
      <p className="text-text-secondary mb-6 ml-12 text-sm">{subtitle}</p>

      <form onSubmit={handleSubmit} className="space-y-4">
        {/* URL */}
        <div>
          <label className="text-text-secondary mb-1.5 block text-xs font-medium tracking-wider uppercase">
            URL
          </label>
          <input
            type="url"
            value={url}
            onChange={(e) => {
              setUrl(e.target.value);
              setTestState("idle");
            }}
            placeholder="http://localhost:7878"
            className="rounded-button border-border bg-surface text-text-primary placeholder:text-text-secondary/50 focus:border-accent/60 w-full border px-3 py-2.5 text-sm transition-colors focus:outline-none"
          />
        </div>

        {/* API Key */}
        <div>
          <label className="text-text-secondary mb-1.5 block text-xs font-medium tracking-wider uppercase">
            Clé API
          </label>
          <input
            type="text"
            value={apiKey}
            onChange={(e) => {
              setApiKey(e.target.value);
              setTestState("idle");
            }}
            placeholder="••••••••••••••••••••••••••••••••"
            className="rounded-button border-border bg-surface text-text-primary placeholder:text-text-secondary/50 focus:border-accent/60 w-full border px-3 py-2.5 font-mono text-sm transition-colors focus:outline-none"
          />
        </div>

        {/* Bouton Test */}
        <button
          type="button"
          onClick={handleTest}
          disabled={!url || !apiKey || testState === "testing"}
          className={cn(
            "rounded-button flex w-full items-center justify-center gap-2 border px-4 py-2.5 text-sm font-medium transition-all",
            testState === "ok"
              ? "border-success/40 bg-success/10 text-success"
              : testState === "error"
                ? "border-swipe-skip/40 bg-swipe-skip/10 text-swipe-skip"
                : "border-border bg-surface text-text-secondary hover:text-text-primary hover:border-accent/40 disabled:cursor-not-allowed disabled:opacity-40",
          )}
        >
          {testState === "testing" ? (
            <>
              <span className="h-3.5 w-3.5 animate-spin rounded-full border-2 border-current border-t-transparent" />
              Test en cours…
            </>
          ) : testState === "ok" ? (
            <>
              <CircleCheck className="size-4" /> Connexion réussie
            </>
          ) : testState === "error" ? (
            <>
              <AlertTriangle className="size-4" /> {testError ?? "Échec"}
            </>
          ) : (
            <>
              <Plug className="size-4" /> Tester la connexion
            </>
          )}
        </button>

        {error && <p className="text-swipe-skip text-xs">{error}</p>}

        {/* Actions */}
        <div className="flex gap-2 pt-1">
          <button
            type="submit"
            disabled={submitting || !url || !apiKey}
            className="rounded-button bg-accent hover:bg-accent/90 flex flex-1 items-center justify-center gap-2 px-4 py-2.5 text-sm font-medium text-white transition-all disabled:cursor-not-allowed disabled:opacity-40"
          >
            {submitting ? (
              <span className="h-3.5 w-3.5 animate-spin rounded-full border-2 border-white border-t-transparent" />
            ) : (
              "Enregistrer"
            )}
          </button>
        </div>

        {/* Skip */}
        <button
          type="button"
          onClick={onSkip}
          className="text-text-secondary/60 hover:text-text-secondary w-full py-1 text-xs transition-colors"
        >
          Passer cette étape →
        </button>
      </form>
    </div>
  );
}
