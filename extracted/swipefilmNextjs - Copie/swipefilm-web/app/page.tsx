"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useAppStore } from "@/lib/store";
import { fetchMe, getServers } from "@/lib/auth";
import { getToken } from "@/lib/api";

export default function RootPage() {
  const router = useRouter();
  const { user, setUser, activeServer, setActiveServer, setServers } = useAppStore();
  const [checking, setChecking] = useState(true);

  useEffect(() => {
    async function check() {
      const token = getToken();

      // Pas de token → login
      if (!token) {
        router.replace("/login");
        return;
      }

      try {
        // Token présent mais pas de user en store → fetch depuis l'API
        let currentUser = user;
        if (!currentUser) {
          const me = await fetchMe();
          currentUser = {
            id: me.id,
            email: me.email,
            name: (me as { displayName?: string }).displayName ?? me.email,
          };
          setUser(currentUser);
        }

        // Vérifie les serveurs — store ou API
        let currentServer = activeServer;
        if (!currentServer) {
          const servers = await getServers();
          const mediaServer = servers.find(
            (s) => s.type === "jellyfin" || s.type === "plex"
          );

          if (mediaServer) {
            setServers(servers);
            setActiveServer(mediaServer);
            currentServer = mediaServer;
          }
        }

        // Décision finale
        if (currentServer) {
          router.replace("/home");
        } else {
          router.replace("/onboarding");
        }
      } catch {
        // Token invalide ou expiré
        router.replace("/login");
      }
    }

    check().finally(() => setChecking(false));
  }, []);

  if (checking) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="h-8 w-8 rounded-full border-2 border-accent border-t-transparent animate-spin" />
      </div>
    );
  }

  return null;
}
