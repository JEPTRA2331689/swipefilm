"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Users } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { createSession } from "@/features/session/api";

export function CreateSessionButton() {
  const router = useRouter();
  const [loading, setLoading] = useState(false);

  async function handleClick() {
    setLoading(true);
    try {
      const session = await createSession();
      router.push(`/session/${session.code}`);
    } catch {
      setLoading(false);
    }
  }

  return (
    <Button
      variant="accent-soft"
      size="sm"
      onClick={handleClick}
      disabled={loading}
      className="rounded-pill px-4 py-1.5 text-[13px]"
    >
      <Users className="size-4" aria-hidden />
      {loading ? "Création…" : "Session groupe"}
    </Button>
  );
}
