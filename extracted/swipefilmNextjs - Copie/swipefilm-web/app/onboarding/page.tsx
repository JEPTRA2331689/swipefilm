"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { PosterBackdrop } from "@/components/onboarding/PosterBackdrop";
import { WelcomeStep } from "@/components/onboarding/WelcomeStep";
import { ConnectForm } from "@/components/onboarding/ConnectForm";
import { addServer, configureSeerr } from "@/lib/auth";
import { useAppStore } from "@/lib/store";

type Step = "welcome" | "media-server" | "overseerr";

export default function OnboardingPage() {
  const router = useRouter();
  const setActiveServer = useAppStore((s) => s.setActiveServer);
  const setServers = useAppStore((s) => s.setServers);
  const [step, setStep] = useState<Step>("welcome");

  async function handleMediaServerConnect(data: {
    type: string;
    friendlyName: string;
    url: string;
    username: string;
    password: string;
  }) {
    const server = await addServer({
      friendlyName: data.friendlyName,
      type: data.type as "plex" | "jellyfin",
      username: data.username,
      password: data.password,
      url: data.url,
    });

    setServers([server]);
    setActiveServer(server);
    setStep("overseerr");
  }

  async function handleOverseerrConnect(data: {
    type: string;
    friendlyName: string;
    url: string;
    username: string;
    password: string;
  }) {
    // Pour Overseerr : username = clé API, url = adresse
    await configureSeerr(data.url, data.username);
    router.replace("/home");
  }

  return (
    <div className="relative min-h-screen flex items-center justify-center px-4 py-12">
      <PosterBackdrop />

      {step === "welcome" && (
        <WelcomeStep onNext={() => setStep("media-server")} />
      )}

      {step === "media-server" && (
        <ConnectForm
          title="Connexion à votre serveur média"
          options={[
            { id: "jellyfin", label: "Jellyfin" },
            { id: "plex", label: "Plex" },
          ]}
          onSubmit={handleMediaServerConnect}
          onBack={() => setStep("welcome")}
        />
      )}

      {step === "overseerr" && (
        <ConnectForm
          title="Configurer Overseerr"
          subtitle="Facultatif — permet de requêter des films directement depuis SwipeFilm."
          options={[{ id: "overseerr", label: "Overseerr" }]}
          onSubmit={handleOverseerrConnect}
          onBack={() => setStep("media-server")}
          submitLabel="Terminer"
          skipLabel="Passer cette étape"
          onSkip={() => router.replace("/home")}
        />
      )}
    </div>
  );
}
