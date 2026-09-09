"use client";

import { useState, useEffect } from "react";
import { CircleCheck } from "lucide-react";
import {
  getDefaultPermissions,
  updateDefaultPermissions,
} from "@/features/users/api";
import { PERMISSION_BITS } from "@/features/users/constants";
import { PermToggle } from "@/features/users/components/PermToggle";
import type { Permission } from "@/lib/permissions";
import {
  SectionCard,
  CardBody,
  CardFooter,
  BtnPrimary,
  Spinner,
} from "@/shared/ui/FormPrimitives";

export function DefaultPermissionsSection() {
  const [perms, setPerms] = useState<number>(0);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    getDefaultPermissions()
      .then((d) => setPerms(d.defaultPermissions))
      .finally(() => setLoading(false));
  }, []);

  function toggle(bit: Permission) {
    setPerms((p) => ((p & bit) !== 0 ? p & ~bit : p | bit));
    setSaved(false);
  }

  async function handleSave() {
    setSaving(true);
    try {
      await updateDefaultPermissions(perms);
      setSaved(true);
      setTimeout(() => setSaved(false), 2000);
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
        {PERMISSION_BITS.map(({ value, label, description }) => (
          <PermToggle
            key={value}
            label={label}
            description={description}
            checked={(perms & value) !== 0}
            onChange={() => toggle(value)}
          />
        ))}
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
