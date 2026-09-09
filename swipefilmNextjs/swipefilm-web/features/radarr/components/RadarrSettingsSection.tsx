"use client";

import { Clapperboard } from "lucide-react";
import { ArrConnectionSection } from "@/shared/ui/ArrConnectionSection";
import {
  getRadarr,
  updateRadarr,
  getRadarrQualityProfiles,
  getRadarrRootFolders,
  verifyRadarrWebhook,
} from "@/features/radarr/api";

export function RadarrSettingsSection() {
  return (
    <ArrConnectionSection
      label="Radarr"
      description="Téléchargement automatique de films"
      icon={Clapperboard}
      urlPlaceholder="http://localhost:7878"
      onGet={getRadarr}
      onUpdate={updateRadarr}
      onGetProfiles={getRadarrQualityProfiles}
      onGetFolders={getRadarrRootFolders}
      onVerifyWebhook={verifyRadarrWebhook}
    />
  );
}
