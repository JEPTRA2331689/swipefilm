"use client";

import { useState } from "react";
import { Crown, UserRound } from "lucide-react";
import { Button } from "@/components/ui/Button";
import { launchSession, type SessionMember } from "@/features/session/api";
import { JoinQrCode } from "@/features/session/components/JoinQrCode";

// ✅ Seul écran où le QR/code apparaît — une fois l'hôte lancé, on passe à
// la configuration (genre, durée) puis au swipe, sans distraction.
export function SessionLobby({
  code,
  members,
  isHost,
  onLaunched,
}: {
  code: string;
  members: SessionMember[];
  isHost: boolean;
  onLaunched: () => void;
}) {
  const [launching, setLaunching] = useState(false);

  async function handleLaunch() {
    setLaunching(true);
    try {
      await launchSession(code);
      onLaunched();
    } finally {
      setLaunching(false);
    }
  }

  return (
    <div className="mx-auto flex w-full max-w-2xl flex-col items-center gap-8 px-4 py-8">
      <JoinQrCode code={code} />

      <div className="w-full">
        <h2 className="font-display text-text-primary mb-3 text-center text-sm font-semibold">
          {members.length} participant{members.length !== 1 ? "s" : ""}
        </h2>
        <div className="flex flex-wrap justify-center gap-2">
          {members.map((m) => (
            <span
              key={m.userId}
              className="bg-surface-alt text-text-secondary flex items-center gap-1.5 rounded-pill px-3 py-1.5 text-sm"
            >
              {m.isHost ? (
                <Crown className="text-accent size-3.5" aria-hidden />
              ) : (
                <UserRound className="size-3.5 opacity-60" aria-hidden />
              )}
              {m.displayName}
            </span>
          ))}
        </div>
      </div>

      {isHost ? (
        <Button
          variant="primary"
          className="w-full max-w-xs"
          onClick={handleLaunch}
          disabled={launching}
        >
          {launching ? "Lancement…" : "Lancer"}
        </Button>
      ) : (
        <p className="text-text-secondary text-center text-sm">
          En attente que l&apos;hôte lance la session…
        </p>
      )}
    </div>
  );
}
