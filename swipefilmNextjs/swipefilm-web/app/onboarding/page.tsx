"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { motion, AnimatePresence } from "framer-motion";
import { PosterBackdrop } from "@/components/onboarding/PosterBackdrop";
import { WelcomeStep } from "@/components/onboarding/WelcomeStep";
import { ConnectForm } from "@/components/onboarding/ConnectForm";
import { AuthForm } from "@/components/auth/AuthForm";
import {
  setupAdmin,
  setupServer,
  setupRadarr,
  skipRadarr,
  setupSonarr,
  skipSonarr,
  testRadarr,
  testSonarr,
  fetchMe,
  normalizeServer,
} from "@/lib/auth";
import { useAppStore } from "@/lib/store";
import { cn } from "@/lib/utils";
import type { UserServer } from "@/types";

type Step = "welcome" | "create-admin" | "media-server" | "radarr" | "sonarr";

const STEPS: Step[] = [
  "welcome",
  "create-admin",
  "media-server",
  "radarr",
  "sonarr",
];
const STEP_LABELS: Record<Step, string> = {
  welcome: "Bienvenue",
  "create-admin": "Admin",
  "media-server": "Serveur",
  radarr: "Radarr",
  sonarr: "Sonarr",
};

export default function OnboardingPage() {
  const router = useRouter();
  const { setUser, setServer } = useAppStore();
  const [step, setStep] = useState<Step>("welcome");

  // ── Handlers ──────────────────────────────────────────────────────

  async function handleCreateAdmin(values: Record<string, string>) {
    if (values.password !== values.confirmPassword) {
      throw new Error("Les mots de passe ne correspondent pas.");
    }
    // ✅ setupAdmin pose déjà le cookie de token à partir de la réponse
    // backend (CreateAdmin renvoie directement un Token) — pas besoin d'un
    // second login() (qui, en plus, ne peut pas être appelé ici : useAuth()
    // est un hook React, interdit dans un handler).
    await setupAdmin(values.email, values.password, values.displayName);
    const me = await fetchMe();
    setUser({
      id: me.id,
      email: me.email,
      name: (me as { displayName?: string }).displayName ?? me.email,
    });
    setStep("media-server");
  }

  async function handleMediaServer(data: {
    type: string;
    friendlyName: string;
    url: string;
    apiKey?: string;
    username?: string;
    password?: string;
  }) {
    await setupServer({
      friendlyName: data.friendlyName,
      type: data.type === "jellyfin" ? 1 : 0, // ServerType enum: Plex=0, Jellyfin=1
      url: data.url,
      apiKey: data.apiKey,
      username: data.username,
      password: data.password,
    });
    const me = await fetchMe();
    const raw = (me as { server?: UserServer | null }).server ?? null;
    setServer(raw ? normalizeServer(raw) : null);
    setStep("radarr");
  }

  async function handleRadarr(data: { url: string; apiKey: string }) {
    await setupRadarr(data.url, data.apiKey);
    setStep("sonarr");
  }

  async function handleSonarr(data: { url: string; apiKey: string }) {
    await setupSonarr(data.url, data.apiKey);
    router.replace("/home");
  }

  // ── Stepper ───────────────────────────────────────────────────────
  const visibleSteps = STEPS.slice(1);

  return (
    <div className="relative flex min-h-screen flex-col items-center justify-center px-4 py-12">
      <PosterBackdrop />

      {/* Progress bar */}
      {step !== "welcome" && (
        <div className="bg-bg-primary/80 fixed top-0 right-0 left-0 z-50 px-6 pt-4 pb-2 backdrop-blur-sm">
          <div className="mx-auto flex max-w-xl items-center gap-1.5">
            {visibleSteps.map((s, i) => {
              const idx = i + 1;
              const current = STEPS.indexOf(step);
              const done = current > idx;
              const active = current === idx;
              return (
                <div
                  key={s}
                  className="flex flex-1 flex-col items-center gap-1"
                >
                  <div
                    className={cn(
                      "h-1 w-full rounded-full transition-all duration-300",
                      done
                        ? "bg-accent"
                        : active
                          ? "bg-accent/60"
                          : "bg-border",
                    )}
                  />
                  <span
                    className={cn(
                      "text-[9px] tracking-wider uppercase transition-colors",
                      active
                        ? "text-accent"
                        : done
                          ? "text-accent/60"
                          : "text-text-secondary/40",
                    )}
                  >
                    {STEP_LABELS[s]}
                  </span>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {/* Steps */}
      <AnimatePresence mode="wait">
        <motion.div
          key={step}
          initial={{ opacity: 0, y: 12 }}
          animate={{ opacity: 1, y: 0 }}
          exit={{ opacity: 0, y: -12 }}
          transition={{ duration: 0.2 }}
          className="flex w-full justify-center"
        >
          {step === "welcome" && (
            <WelcomeStep onNext={() => setStep("create-admin")} />
          )}

          {step === "create-admin" && (
            <AuthForm
              title="Créer votre compte admin"
              subtitle="Ce compte aura accès à toutes les fonctionnalités de SwipeFilm."
              fields={[
                {
                  name: "displayName",
                  label: "Nom affiché",
                  type: "text",
                  placeholder: "Jean Dupont",
                },
                {
                  name: "email",
                  label: "Adresse e-mail",
                  type: "email",
                  placeholder: "vous@exemple.com",
                },
                {
                  name: "password",
                  label: "Mot de passe",
                  type: "password",
                  placeholder: "Minimum 8 caractères",
                },
                {
                  name: "confirmPassword",
                  label: "Confirmer le mot de passe",
                  type: "password",
                  placeholder: "••••••••",
                },
              ]}
              submitLabel="Créer mon compte"
              footerText="Déjà un compte ?"
              footerLinkLabel="Se connecter"
              footerLinkHref="/login"
              onSubmit={handleCreateAdmin}
            />
          )}

          {step === "media-server" && (
            <ConnectForm
              title="Connexion à votre serveur média"
              options={[
                { id: "jellyfin", label: "Jellyfin" },
                { id: "plex", label: "Plex" },
              ]}
              onSubmit={handleMediaServer}
              onBack={() => setStep("create-admin")}
            />
          )}

          {step === "radarr" && (
            <ArrSetupStep
              title="Configurer Radarr"
              subtitle="Facultatif — pour télécharger des films automatiquement."
              icon="ph-video"
              onTest={(url, apiKey) => testRadarr(url, apiKey)}
              onSubmit={handleRadarr}
              onSkip={async () => {
                await skipRadarr();
                setStep("sonarr");
              }}
              onBack={() => setStep("media-server")}
            />
          )}

          {step === "sonarr" && (
            <ArrSetupStep
              title="Configurer Sonarr"
              subtitle="Facultatif — pour télécharger des séries automatiquement."
              icon="ph-television"
              onTest={(url, apiKey) => testSonarr(url, apiKey)}
              onSubmit={handleSonarr}
              onSkip={async () => {
                await skipSonarr();
                router.replace("/home");
              }}
              onBack={() => setStep("radarr")}
            />
          )}
        </motion.div>
      </AnimatePresence>
    </div>
  );
}

/* ── Composant ArrSetupStep (Radarr / Sonarr) ── */

interface ArrSetupStepProps {
  title: string;
  subtitle: string;
  icon: string;
  onTest: (url: string, apiKey: string) => Promise<void>;
  onSubmit: (data: { url: string; apiKey: string }) => Promise<void>;
  onSkip: () => Promise<void>;
  onBack: () => void;
}

function ArrSetupStep({
  title,
  subtitle,
  icon,
  onTest,
  onSubmit,
  onSkip,
  onBack,
}: ArrSetupStepProps) {
  const [url, setUrl] = useState("");
  const [apiKey, setApiKey] = useState("");
  const [testState, setTestState] = useState<
    "idle" | "testing" | "ok" | "error"
  >("idle");
  const [testError, setTestError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleTest() {
    if (!url || !apiKey) return;
    console.log("Testing connection to", url, "with API key", apiKey);
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
    } catch (e) {
      setError(e instanceof Error ? e.message : "Erreur");
      setSubmitting(false);
    }
  }

  return (
    <div className="relative z-10 w-full max-w-md">
      <div className="rounded-card border-border bg-bg-primary/90 border p-6 shadow-2xl backdrop-blur-md sm:p-8">
        {/* Header */}
        <div className="mb-1 flex items-center gap-3">
          <div className="bg-accent/10 border-accent/20 flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-full border">
            <i
              className={cn("ph-thin text-accent text-lg", icon)}
              aria-hidden
            />
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
                <i className="ph-thin ph-check-circle text-base" /> Connexion
                réussie
              </>
            ) : testState === "error" ? (
              <>
                <i className="ph-thin ph-warning text-base" />{" "}
                {testError ?? "Échec"}
              </>
            ) : (
              <>
                <i className="ph-thin ph-plug text-base" /> Tester la connexion
              </>
            )}
          </button>

          {error && <p className="text-swipe-skip text-xs">{error}</p>}

          {/* Actions */}
          <div className="flex gap-2 pt-1">
            <button
              type="button"
              onClick={onBack}
              className="rounded-button border-border bg-surface text-text-secondary hover:text-text-primary flex items-center gap-1.5 border px-4 py-2.5 text-sm transition-colors"
            >
              <i className="ph-thin ph-arrow-left text-base" />
              Retour
            </button>
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
    </div>
  );
}
