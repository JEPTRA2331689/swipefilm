"use client";

import { Video } from "lucide-react";
import { ArrOnboardingStep } from "@/shared/ui/ArrOnboardingStep";
import { testRadarr, setupRadarr, verifyRadarrWebhook } from "@/features/radarr/api";

export function RadarrOnboardingStep({
  onContinue,
  onSkip,
}: {
  onContinue: () => void;
  onSkip: () => Promise<void>;
}) {
  return (
    <ArrOnboardingStep
      title="Configurer Radarr"
      subtitle="Facultatif — pour télécharger des films automatiquement."
      icon={Video}
      onTest={testRadarr}
      onSubmit={(data) => setupRadarr(data.url, data.apiKey)}
      onVerifyWebhook={verifyRadarrWebhook}
      onContinue={onContinue}
      onSkip={onSkip}
    />
  );
}
