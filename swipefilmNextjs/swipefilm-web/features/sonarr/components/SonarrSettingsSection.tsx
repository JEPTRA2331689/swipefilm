"use client";

import { Tv } from "lucide-react";
import { ArrConnectionSection } from "@/shared/ui/ArrConnectionSection";
import {
  getSonarr,
  updateSonarr,
  getSonarrQualityProfiles,
  getSonarrRootFolders,
  verifySonarrWebhook,
} from "@/features/sonarr/api";

export function SonarrSettingsSection() {
  return (
    <ArrConnectionSection
      label="Sonarr"
      description="Téléchargement automatique de séries"
      icon={Tv}
      urlPlaceholder="http://localhost:8989"
      onGet={getSonarr}
      onUpdate={updateSonarr}
      onGetProfiles={getSonarrQualityProfiles}
      onGetFolders={getSonarrRootFolders}
      onVerifyWebhook={verifySonarrWebhook}
    />
  );
}
