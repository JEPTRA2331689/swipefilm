"use client";

import { useState, useEffect } from "react";
import { CircleCheck } from "lucide-react";
import { getPublicUrl, setPublicUrl } from "@/features/webhook/api";
import {
  SectionCard,
  CardBody,
  CardFooter,
  FieldLabel,
  FieldInput,
  BtnPrimary,
  Spinner,
  type SaveState,
} from "@/shared/ui/FormPrimitives";

// ✅ Adresse explicite que Radarr/Sonarr utilisent pour joindre SwipeFilm —
// jamais devinée automatiquement (voir onboarding, étape "Adresse").
// Modifiable ici après le setup, ex: si l'IP LAN change.

export function WebhookSettingsSection() {
  const [url, setUrl] = useState("");
  const [saved, setSaved] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [saveState, setSaveState] = useState<SaveState>("idle");
  const [saveError, setSaveError] = useState<string | null>(null);

  useEffect(() => {
    getPublicUrl()
      .then((u) => {
        setUrl(u ?? "");
        setSaved(u);
      })
      .finally(() => setLoading(false));
  }, []);

  async function handleSave() {
    setSaveState("saving");
    setSaveError(null);
    try {
      const result = await setPublicUrl(url);
      setSaved(result);
      setSaveState("ok");
    } catch (e) {
      setSaveState("error");
      setSaveError(e instanceof Error ? e.message : "Erreur");
    }
  }

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
      <CardBody className="space-y-5">
        <div>
          <FieldLabel>Adresse publique</FieldLabel>
          <FieldInput
            type="url"
            value={url}
            onChange={(e) => {
              setUrl(e.target.value);
              setSaveState("idle");
            }}
            placeholder="http://192.168.1.10:5250"
          />
          <p className="text-text-secondary mt-2 text-xs">
            L&apos;adresse que Radarr et Sonarr utilisent pour contacter
            SwipeFilm (webhooks de téléchargement). Doit être joignable
            depuis leurs serveurs — pas <code>localhost</code>.
          </p>
        </div>

        {saveState === "error" && saveError && (
          <p className="text-sm text-red-400">{saveError}</p>
        )}
        {saveState === "ok" && (
          <p className="flex items-center gap-1.5 text-sm text-green-400">
            <CircleCheck className="size-4" />
            Adresse enregistrée.
          </p>
        )}

        {saved && (
          <p className="text-text-secondary text-xs">
            Après une modification, utilise le bouton{" "}
            <span className="text-text-primary font-medium">
              Vérifier le webhook
            </span>{" "}
            dans les onglets Radarr et Sonarr pour réenregistrer avec la
            nouvelle adresse.
          </p>
        )}
      </CardBody>

      <CardFooter>
        <BtnPrimary onClick={handleSave} disabled={saveState === "saving"}>
          {saveState === "saving" ? (
            <>
              <Spinner />
              Enregistrement…
            </>
          ) : (
            "Enregistrer"
          )}
        </BtnPrimary>
      </CardFooter>
    </SectionCard>
  );
}
