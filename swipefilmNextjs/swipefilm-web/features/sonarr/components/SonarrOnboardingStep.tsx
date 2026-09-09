"use client";

import { Tv } from "lucide-react";
import { ArrOnboardingStep } from "@/shared/ui/ArrOnboardingStep";
import { testSonarr, setupSonarr, verifySonarrWebhook } from "@/features/sonarr/api";

export function SonarrOnboardingStep({
  onContinue,
  onSkip,
}: {
  onContinue: () => void;
  onSkip: () => Promise<void>;
}) {
  return (
    <ArrOnboardingStep
      title="Configurer Sonarr"
      subtitle="Facultatif — pour télécharger des séries automatiquement."
      icon={Tv}
      onTest={testSonarr}
      onSubmit={(data) => setupSonarr(data.url, data.apiKey)}
      onVerifyWebhook={verifySonarrWebhook}
      onContinue={onContinue}
      onSkip={onSkip}
    />
  );
}
