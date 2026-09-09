"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useAppStore } from "@/lib/store";
import { fetchMe, checkSetupStatus } from "@/features/auth/api";
import { normalizeServer } from "@/features/media-server/api";

export default function RootPage() {
  const router = useRouter();
  const { setUser, setServer } = useAppStore();
  const [checking, setChecking] = useState(true);

  useEffect(() => {
    async function check() {
      try {
        const status = await checkSetupStatus();
        if (!status.isSetupComplete) {
          router.replace("/onboarding");
          return;
        }
      } catch {
        // Si l'endpoint échoue, on assume que le setup est fait
      }

      // ✅ Cookie de session HttpOnly — pas de présence à vérifier côté
      // client, fetchMe() échoue (401) si non connecté.
      try {
        const me = await fetchMe();
        setUser(me);
        setServer(me.server ? normalizeServer(me.server) : null);

        router.replace("/home");
      } catch {
        router.replace("/login");
      }
    }

    check().finally(() => setChecking(false));
  }, []);

  if (checking) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <div className="border-accent h-8 w-8 animate-spin rounded-full border-2 border-t-transparent" />
      </div>
    );
  }

  return null;
}
