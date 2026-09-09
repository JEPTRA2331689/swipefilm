"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { SetupBackdrop } from "@/components/onboarding/SetupBackdrop";
import { SetupSteps } from "@/components/onboarding/SetupSteps";
import { ConnectForm } from "@/components/onboarding/ConnectForm";
import { SyncingScreen } from "@/components/onboarding/SyncingScreen";
import { AuthForm } from "@/features/auth/components/AuthForm";
import { PublicUrlStep } from "@/features/webhook/components/PublicUrlStep";
import { setupPublicUrl, skipPublicUrl } from "@/features/webhook/api";
import { RadarrOnboardingStep } from "@/features/radarr/components/RadarrOnboardingStep";
import { skipRadarr } from "@/features/radarr/api";
import { SonarrOnboardingStep } from "@/features/sonarr/components/SonarrOnboardingStep";
import { skipSonarr } from "@/features/sonarr/api";
import { setupAdmin, fetchMe } from "@/features/auth/api";
import { setupServer, normalizeServer } from "@/features/media-server/api";
import { useAppStore } from "@/lib/store";
import { cn } from "@/lib/utils";
import type { UserServer } from "@/types";

// ✅ Structure reprise d'Overseerr (src/components/Setup/index.tsx, MIT) —
// cercles d'étapes + une seule carte de contenu, pas d'écran "Bienvenue" à
// part (fondu dans l'étape 1, comme LoginWithPlex chez eux). "syncing" n'est
// pas une étape de config — écran de chargement plein cadre après Sonarr,
// pas affiché dans la nav (voir STEPS plus bas, qui l'exclut).
type Step =
  | "admin"
  | "public-url"
  | "media-server"
  | "radarr"
  | "sonarr"
  | "syncing";

const STEPS: { key: Step; label: string }[] = [
  { key: "admin", label: "Compte admin" },
  { key: "public-url", label: "Adresse" },
  { key: "media-server", label: "Serveur" },
  { key: "radarr", label: "Radarr" },
  { key: "sonarr", label: "Sonarr" },
];

export default function OnboardingPage() {
  const router = useRouter();
  const { setUser, setServer } = useAppStore();
  const [step, setStep] = useState<Step>("admin");
  const stepIndex = STEPS.findIndex((s) => s.key === step);

  // ── Handlers ──────────────────────────────────────────────────────

  async function handleCreateAdmin(values: Record<string, string>) {
    if (values.password !== values.confirmPassword) {
      throw new Error("Les mots de passe ne correspondent pas.");
    }
    // ✅ setupAdmin connecte déjà (cookie de session posé par le backend) —
    // pas besoin d'un second login() (qui, en plus, ne peut pas être appelé
    // ici : useAuth() est un hook React, interdit dans un handler).
    await setupAdmin(values.email, values.password, values.displayName);
    const me = await fetchMe();
    setUser(me);
    setStep("public-url");
  }

  async function handlePublicUrl(url: string) {
    await setupPublicUrl(url);
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

  return (
    <div className="relative flex min-h-screen flex-col justify-center px-4 py-12">
      <SetupBackdrop />

      <div className="relative z-10 mx-auto w-full max-w-2xl">
        {/* Logo */}
        <div className="mb-10 flex justify-center">
          <span className="font-display text-text-primary text-3xl font-bold tracking-tight">
            Swipe<span className="text-accent">Film</span>
          </span>
        </div>

        {/* Nav d'étapes — cachée pendant l'écran de sync, ce n'est pas une
        étape de config */}
        {step !== "syncing" && (
          <nav>
            <ul
              className="border-border bg-surface/50 divide-border rounded-card divide-y border md:flex md:divide-y-0"
              style={{ backdropFilter: "blur(5px)" }}
            >
              {STEPS.map((s, i) => (
                <SetupSteps
                  key={s.key}
                  stepNumber={i + 1}
                  description={s.label}
                  active={step === s.key}
                  completed={stepIndex > i}
                  isLastStep={i === STEPS.length - 1}
                />
              ))}
            </ul>
          </nav>
        )}

        {/* Carte de contenu — une seule, le contenu change selon l'étape */}
        <div
          className={cn(
            "rounded-card border-border bg-surface/50 w-full border p-6 sm:p-8",
            step !== "syncing" && "mt-6",
          )}
          style={{ backdropFilter: "blur(5px)" }}
        >
          {step === "admin" && (
            <>
              <div className="mb-6 text-center">
                <h2 className="font-display text-text-primary text-2xl font-semibold">
                  Bienvenue sur SwipeFilm
                </h2>
                <p className="text-text-secondary mt-1.5 text-sm">
                  Des films qui vous ressemblent — commençons par créer votre
                  compte admin.
                </p>
              </div>
              <AuthForm
                bare
                title="Compte admin"
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
                onSubmit={handleCreateAdmin}
              />
            </>
          )}

          {step === "public-url" && (
            <PublicUrlStep
              onSubmit={handlePublicUrl}
              onSkip={async () => {
                await skipPublicUrl();
                setStep("media-server");
              }}
            />
          )}

          {step === "media-server" && (
            <ConnectForm
              bare
              title="Connexion à votre serveur média"
              options={[
                { id: "jellyfin", label: "Jellyfin" },
                { id: "plex", label: "Plex" },
              ]}
              onSubmit={handleMediaServer}
            />
          )}

          {step === "radarr" && (
            <RadarrOnboardingStep
              onContinue={() => setStep("sonarr")}
              onSkip={async () => {
                await skipRadarr();
                setStep("sonarr");
              }}
            />
          )}

          {step === "sonarr" && (
            <SonarrOnboardingStep
              onContinue={() => setStep("syncing")}
              onSkip={async () => {
                await skipSonarr();
                setStep("syncing");
              }}
            />
          )}

          {step === "syncing" && (
            <SyncingScreen onDone={() => router.replace("/home")} />
          )}
        </div>
      </div>
    </div>
  );
}
