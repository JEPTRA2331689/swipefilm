"use client";

import { useState } from "react";
import { CircleCheck } from "lucide-react";
import { changePassword } from "@/features/profile/api";
import {
  SectionCard,
  CardBody,
  CardFooter,
  FieldLabel,
  FieldInput,
  BtnPrimary,
  Spinner,
} from "@/shared/ui/FormPrimitives";

export function MyPasswordSection() {
  const [current, setCurrent] = useState("");
  const [next, setNext] = useState("");
  const [confirm, setConfirm] = useState("");
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleSave(e: React.FormEvent) {
    e.preventDefault();
    if (next !== confirm) {
      setError("Les mots de passe ne correspondent pas.");
      return;
    }
    setSaving(true);
    setError(null);
    try {
      await changePassword(current, next);
      setCurrent("");
      setNext("");
      setConfirm("");
      setSaved(true);
      setTimeout(() => setSaved(false), 2000);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Erreur");
    } finally {
      setSaving(false);
    }
  }

  return (
    <SectionCard>
      <form onSubmit={handleSave}>
        <CardBody className="space-y-5">
          <div>
            <FieldLabel>Mot de passe actuel</FieldLabel>
            <FieldInput
              type="password"
              value={current}
              onChange={(e) => setCurrent(e.target.value)}
              required
            />
          </div>
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
            <div>
              <FieldLabel>Nouveau mot de passe</FieldLabel>
              <FieldInput
                type="password"
                value={next}
                onChange={(e) => setNext(e.target.value)}
                required
              />
            </div>
            <div>
              <FieldLabel>Confirmer le mot de passe</FieldLabel>
              <FieldInput
                type="password"
                value={confirm}
                onChange={(e) => setConfirm(e.target.value)}
                required
              />
            </div>
          </div>
          {error && <p className="text-sm text-red-400">{error}</p>}
        </CardBody>
        <CardFooter>
          {saved && (
            <span className="mr-auto flex items-center gap-1.5 text-sm text-green-400">
              <CircleCheck className="size-4" /> Modifié
            </span>
          )}
          <BtnPrimary type="submit" disabled={saving}>
            {saving && <Spinner />}
            Modifier le mot de passe
          </BtnPrimary>
        </CardFooter>
      </form>
    </SectionCard>
  );
}
