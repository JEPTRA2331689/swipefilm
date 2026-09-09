"use client";

import { useState, useEffect } from "react";
import { CircleCheck } from "lucide-react";
import {
  getMyProfile,
  updateMyProfile,
  type UserProfile,
} from "@/features/profile/api";
import {
  SectionCard,
  CardBody,
  CardFooter,
  FieldLabel,
  FieldInput,
  BtnPrimary,
  Spinner,
} from "@/shared/ui/FormPrimitives";

export function MyProfileSection() {
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [loading, setLoading] = useState(true);
  const [displayName, setDisplayName] = useState("");
  const [email, setEmail] = useState("");
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getMyProfile()
      .then((p) => {
        setProfile(p);
        setDisplayName(p.displayName);
        setEmail(p.email);
      })
      .finally(() => setLoading(false));
  }, []);

  async function handleSave() {
    setSaving(true);
    setError(null);
    try {
      const updated = await updateMyProfile({ displayName, email });
      setProfile(updated);
      setSaved(true);
      setTimeout(() => setSaved(false), 2000);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Erreur");
    } finally {
      setSaving(false);
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
      <CardBody>
        {/* Avatar + identité */}
        <div className="border-border mb-6 flex items-center gap-4 border-b pb-6">
          <div className="bg-accent/10 border-accent/30 flex h-16 w-16 flex-shrink-0 items-center justify-center rounded-full border-2">
            <span className="text-accent text-2xl font-bold">
              {(profile?.displayName ?? "?")[0].toUpperCase()}
            </span>
          </div>
          <div>
            <p className="text-text-primary text-base font-semibold">
              {profile?.displayName}
            </p>
            <p className="text-text-secondary text-sm">{profile?.email}</p>
          </div>
        </div>

        <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
          <div>
            <FieldLabel>Nom affiché</FieldLabel>
            <FieldInput
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
            />
          </div>
          <div>
            <FieldLabel>Adresse email</FieldLabel>
            <FieldInput
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
          </div>
        </div>
        {error && <p className="mt-3 text-sm text-red-400">{error}</p>}
      </CardBody>
      <CardFooter>
        {saved && (
          <span className="mr-auto flex items-center gap-1.5 text-sm text-green-400">
            <CircleCheck className="size-4" /> Enregistré
          </span>
        )}
        <BtnPrimary onClick={handleSave} disabled={saving}>
          {saving && <Spinner />}
          Enregistrer
        </BtnPrimary>
      </CardFooter>
    </SectionCard>
  );
}
