"use client";

import { useEffect, useState } from "react";
import { QRCodeSVG } from "qrcode.react";
import { getPublicUrl } from "@/features/webhook/api";

// ✅ Réutilise la même adresse publique que le webhook Radarr/Sonarr — pas
// une deuxième config séparée. Sans adresse publique configurée, on retombe
// sur l'origine du navigateur (suffisant pour un scan sur le même réseau).
export function JoinQrCode({ code }: { code: string }) {
  const [joinUrl, setJoinUrl] = useState<string | null>(null);

  useEffect(() => {
    getPublicUrl()
      .then((url) => setJoinUrl(`${(url ?? window.location.origin).replace(/\/$/, "")}/join/${code}`))
      .catch(() => setJoinUrl(`${window.location.origin}/join/${code}`));
  }, [code]);

  return (
    <div className="flex flex-col items-center gap-3">
      <div className="rounded-lg bg-white p-3">
        {joinUrl ? (
          <QRCodeSVG value={joinUrl} size={160} />
        ) : (
          <div className="h-[160px] w-[160px] animate-pulse bg-neutral-200" />
        )}
      </div>
      <span className="font-display text-text-primary text-3xl font-bold tracking-[0.3em]">
        {code}
      </span>
      <p className="text-text-secondary text-xs">
        Scanne le QR code ou entre le code sur /join
      </p>
    </div>
  );
}
