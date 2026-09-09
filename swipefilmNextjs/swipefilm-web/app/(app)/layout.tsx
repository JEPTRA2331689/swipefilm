"use client";

import { useEffect, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import { api } from "@/lib/api";
import { Permission, hasPermission } from "@/lib/permissions";

// ✅ Repris tel quel de l'ancien middleware.ts (server) — sans PUBLIC_ROUTES
// ni le skip _next/api/favicon, devenus inutiles : ce layout ne s'exécute
// que pour des pages déjà à l'intérieur du groupe (app), jamais pour les
// routes publiques (/, /login, /onboarding) qui vivent en dehors, ni pour
// des requêtes d'assets/API.
const ROUTE_PERMISSIONS: Array<{ pattern: RegExp; permission: Permission }> = [
  { pattern: /^\/admin\/users(\/|$)/, permission: Permission.ManageUsers },
  { pattern: /^\/admin\/servers(\/|$)/, permission: Permission.Admin },
  { pattern: /^\/admin\/permissions(\/|$)/, permission: Permission.ManageUsers },
  { pattern: /^\/admin(\/|$)/, permission: Permission.ViewAdminDashboard },
  { pattern: /^\/settings(\/|$)/, permission: Permission.CanSwipe },
  { pattern: /^\/movie(\/|$)/, permission: Permission.CanSwipe },
  { pattern: /^\/history(\/|$)/, permission: Permission.CanSwipe },
  { pattern: /^\/profile(\/|$)/, permission: Permission.CanSwipe },
  { pattern: /^\/search(\/|$)/, permission: Permission.CanSwipe },
  { pattern: /^\/watchlist(\/|$)/, permission: Permission.CanSwipe },
  { pattern: /^\/swipe(\/|$)/, permission: Permission.CanSwipe },
  { pattern: /^\/session(\/|$)/, permission: Permission.CanSwipe },
  { pattern: /^\/home(\/|$)/, permission: Permission.CanSwipe },
  { pattern: /^\/list(\/|$)/, permission: Permission.CanSwipe },
];

// ✅ Le cookie de session est HttpOnly — plus moyen de décoder un token
// côté client pour savoir si on est connecté/quelles permissions on a.
// Seul le backend le sait : /api/auth/me sert à la fois de vérif de
// session et de source des permissions pour le contrôle de route.
interface MeResponse {
  permissions: number;
}

export default function AppLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const pathname = usePathname();
  const router = useRouter();
  const [checking, setChecking] = useState(true);

  useEffect(() => {
    setChecking(true);

    api
      .get<MeResponse>("/api/auth/me")
      .then((me) => {
        for (const { pattern, permission } of ROUTE_PERMISSIONS) {
          if (pattern.test(pathname)) {
            if (!hasPermission(me.permissions, permission)) {
              router.replace("/unauthorized");
              return;
            }
            break;
          }
        }
        setChecking(false);
      })
      .catch(() => {
        router.replace("/login");
      });
  }, [pathname, router]);

  if (checking) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <div className="border-accent h-8 w-8 animate-spin rounded-full border-2 border-t-transparent" />
      </div>
    );
  }

  return <>{children}</>;
}
