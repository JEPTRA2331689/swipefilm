"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useAppStore } from "@/lib/store";
import { fetchMe, normalizeServer, checkSetupStatus } from "@/lib/auth";
import { getTokenFromCookie } from "@/lib/jwt";
import type { UserServer } from "@/types";

export default function RootPage() {
  const router = useRouter();
  const { setUser, setServer } = useAppStore();
  const [checking, setChecking] = useState(true);

  useEffect(() => {
    async function check() {
      try {
        const status = await checkSetupStatus();
        if (!status.isComplete) {
          router.replace("/onboarding");
          return;
        }
      } catch {
        // Si l'endpoint échoue, on assume que le setup est fait
      }

      const token = getTokenFromCookie();
      if (!token) {
        router.replace("/login");
        return;
      }

      try {
        const me = await fetchMe();

        setUser({
          id: me.id,
          username: me.username,
          name: (me as { displayName?: string }).displayName ?? me.username,
        });

        const raw = (me as { server?: UserServer | null }).server ?? null;
        setServer(raw ? normalizeServer(raw) : null);

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
