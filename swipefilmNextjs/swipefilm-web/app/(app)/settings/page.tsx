"use client";

import { useState, useEffect, useCallback } from "react";
import {
  UserCircle,
  Lock,
  Paintbrush,
  Clapperboard,
  Tv,
  HardDrive,
  ShieldCheck,
  Settings2,
  Terminal,
  Users,
  Layers,
  CheckSquare,
  ChevronDown,
  Webhook,
  type LucideIcon,
} from "lucide-react";
import { TopNav } from "@/components/layout/TopNav";
import { usePermission } from "@/features/auth/usePermission";
import { useAuth } from "@/features/auth/AuthContext";
import { Permission } from "@/lib/permissions";
import { RadarrSettingsSection } from "@/features/radarr/components/RadarrSettingsSection";
import { SonarrSettingsSection } from "@/features/sonarr/components/SonarrSettingsSection";
import { WebhookSettingsSection } from "@/features/webhook/components/WebhookSettingsSection";
import { ServersSection } from "@/features/media-server/components/ServersSection";
import { MyProfileSection } from "@/features/profile/components/MyProfileSection";
import { MyPasswordSection } from "@/features/profile/components/MyPasswordSection";
import { AppearanceSection } from "@/features/profile/components/AppearanceSection";
import { UsersSection } from "@/features/users/components/UsersSection";
import { DefaultPermissionsSection } from "@/features/users/components/DefaultPermissionsSection";
import { JobsSection } from "@/features/jobs/components/JobsSection";
import { LogsSection } from "@/features/logs/components/LogsSection";
import { MyRequestsSection } from "@/features/catalog/components/MyRequestsSection";
import { ManageRequestsSection } from "@/features/catalog/components/ManageRequestsSection";
import { cn } from "@/lib/utils";

// ── Nav items ─────────────────────────────────────────────────────
type SectionId =
  | "profile"
  | "password"
  | "appearance"
  | "radarr"
  | "sonarr"
  | "webhook"
  | "servers"
  | "default-perms"
  | "jobs"
  | "logs"
  | "my-requests"
  | "manage-requests"
  | "users";

interface NavItem {
  id: SectionId;
  label: string;
  icon: LucideIcon;
  description: string;
  permission?: Permission;
  adminOnly?: boolean;
  group: string;
}

const NAV_ITEMS: NavItem[] = [
  {
    id: "profile",
    label: "Mon profil",
    icon: UserCircle,
    description: "Nom affiché et adresse email",
    group: "Compte",
  },
  {
    id: "password",
    label: "Mot de passe",
    icon: Lock,
    description: "Modifier votre mot de passe",
    group: "Compte",
  },
  {
    id: "appearance",
    label: "Apparence",
    icon: Paintbrush,
    description: "Thème clair ou sombre",
    group: "Compte",
  },
  {
    id: "radarr",
    label: "Radarr",
    icon: Clapperboard,
    description: "Téléchargement automatique de films",
    permission: Permission.CanSwipe,
    group: "Téléchargement",
  },
  {
    id: "sonarr",
    label: "Sonarr",
    icon: Tv,
    description: "Téléchargement automatique de séries",
    permission: Permission.CanSwipe,
    group: "Téléchargement",
  },
  {
    id: "webhook",
    label: "Webhook",
    icon: Webhook,
    description: "Adresse publique pour les webhooks Radarr/Sonarr",
    adminOnly: true,
    group: "Téléchargement",
  },
  {
    id: "servers",
    label: "Serveurs média",
    icon: HardDrive,
    description: "Plex et Jellyfin connectés",
    adminOnly: true,
    group: "Administration",
  },
  {
    id: "default-perms",
    label: "Permissions",
    icon: ShieldCheck,
    description: "Droits par défaut des nouveaux utilisateurs",
    adminOnly: true,
    group: "Administration",
  },
  {
    id: "jobs",
    label: "Tâches planifiées",
    icon: Settings2,
    description: "Déclencher manuellement les jobs Hangfire",
    adminOnly: true,
    group: "Administration",
  },
  {
    id: "logs",
    label: "Logs",
    icon: Terminal,
    description: "Journal applicatif — niveau, recherche, pagination",
    adminOnly: true,
    group: "Administration",
  },
  {
    id: "users",
    label: "Utilisateurs",
    icon: Users,
    description: "Gérer les comptes et leurs droits",
    permission: Permission.ManageUsers,
    group: "Administration",
  },
  {
    id: "my-requests",
    label: "Mes requêtes",
    icon: Layers,
    description: "Films que vous avez demandés",
    permission: Permission.CanRequest,
    group: "Requêtes",
  },
  {
    id: "manage-requests",
    label: "Gérer les requêtes",
    icon: CheckSquare,
    description: "Approuver ou refuser les demandes",
    permission: Permission.ManageRequests,
    group: "Requêtes",
  },
];

const SECTION_META: Record<SectionId, { title: string; description: string }> =
  {
    profile: {
      title: "Mon profil",
      description: "Modifiez votre nom affiché et votre adresse email.",
    },
    password: {
      title: "Mot de passe",
      description: "Changez votre mot de passe de connexion.",
    },
    radarr: {
      title: "Radarr",
      description:
        "Connectez Radarr pour télécharger des films automatiquement.",
    },
    sonarr: {
      title: "Sonarr",
      description:
        "Connectez Sonarr pour télécharger des séries automatiquement.",
    },
    webhook: {
      title: "Webhook",
      description:
        "Adresse à laquelle Radarr et Sonarr peuvent joindre SwipeFilm pour leurs notifications.",
    },
    servers: {
      title: "Serveurs média",
      description: "Gérez vos serveurs Plex et Jellyfin.",
    },
    "default-perms": {
      title: "Permissions par défaut",
      description:
        "Ces permissions sont appliquées à chaque nouvel utilisateur à la création de son compte.",
    },
    jobs: {
      title: "Tâches planifiées",
      description:
        "Lancez manuellement un job Hangfire au lieu d'attendre son prochain passage planifié.",
    },
    logs: {
      title: "Logs",
      description:
        "Journal applicatif — filtrez par niveau ou recherchez un terme précis.",
    },
    users: {
      title: "Utilisateurs",
      description:
        "Gérez les comptes utilisateurs, leurs droits et leurs quotas.",
    },
    "my-requests": {
      title: "Mes requêtes",
      description: "Suivez l'état de vos demandes de films et séries.",
    },
    "manage-requests": {
      title: "Gérer les requêtes",
      description: "Approuvez ou refusez les demandes en attente.",
    },
    appearance: {
      title: "Apparence",
      description: "Choisissez entre le thème sombre et le thème clair.",
    },
  };

// ── Page ─────────────────────────────────────────────────────────

export default function SettingsPage() {
  const { loading } = useAuth();
  const isAdmin = usePermission(Permission.Admin);
  const canSwipe = usePermission(Permission.CanSwipe);
  const canManageUsers = usePermission(Permission.ManageUsers);
  const canRequest = usePermission(Permission.CanRequest);
  const canManageRequests = usePermission(Permission.ManageRequests);

  const [active, setActive] = useState<SectionId>("profile");
  const [mobileOpen, setMobileOpen] = useState(false);

  if (loading) return null;

  function isVisible(item: NavItem) {
    if (item.adminOnly) return isAdmin;
    if (item.permission !== undefined) {
      if (item.permission === Permission.CanSwipe) return canSwipe;
      if (item.permission === Permission.ManageUsers) return canManageUsers;
      if (item.permission === Permission.CanRequest) return canRequest;
      if (item.permission === Permission.ManageRequests)
        return canManageRequests;
    }
    return true;
  }

  const visibleItems = NAV_ITEMS.filter(isVisible);
  const groups = [...new Set(visibleItems.map((i) => i.group))];
  const meta = SECTION_META[active];

  return (
    <div className="bg-bg-primary min-h-screen">
      <TopNav />

      <div className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
        {/* ── Mobile: section selector ── */}
        <div className="mb-6 lg:hidden">
          <button
            onClick={() => setMobileOpen((o) => !o)}
            className="rounded-card border-border bg-surface text-text-primary flex w-full items-center justify-between border px-4 py-3 text-sm"
          >
            <span className="flex items-center gap-2">
              {(() => {
                const ActiveIcon = visibleItems.find(
                  (i) => i.id === active,
                )?.icon;
                return ActiveIcon ? (
                  <ActiveIcon className="size-4 text-accent" />
                ) : null;
              })()}
              {visibleItems.find((i) => i.id === active)?.label}
            </span>
            <ChevronDown
              className={cn(
                "size-4 text-text-secondary transition-transform",
                mobileOpen && "rotate-180",
              )}
            />
          </button>
          {mobileOpen && (
            <div className="rounded-card border-border bg-surface mt-1 overflow-hidden border">
              {visibleItems.map((item) => (
                <button
                  key={item.id}
                  onClick={() => {
                    setActive(item.id);
                    setMobileOpen(false);
                  }}
                  className={cn(
                    "border-border flex w-full items-center gap-3 border-b px-4 py-3 text-left text-sm transition-colors last:border-0",
                    active === item.id
                      ? "bg-accent/10 text-accent"
                      : "text-text-secondary hover:bg-surface-alt hover:text-text-primary",
                  )}
                >
                  <item.icon className="size-4 flex-shrink-0" />
                  {item.label}
                </button>
              ))}
            </div>
          )}
        </div>

        <div className="flex gap-8">
          {/* ── Sidebar ── */}
          <aside className="hidden w-64 flex-shrink-0 flex-col lg:flex">
            <nav className="sticky top-24 space-y-6">
              {groups.map((group) => {
                const items = visibleItems.filter((i) => i.group === group);
                return (
                  <div key={group}>
                    <p className="text-text-secondary/50 mb-1 px-3 text-[11px] font-semibold tracking-widest uppercase">
                      {group}
                    </p>
                    <ul className="space-y-0.5">
                      {items.map((item) => (
                        <li key={item.id}>
                          <button
                            onClick={() => setActive(item.id)}
                            className={cn(
                              "flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-left text-sm font-medium transition-all",
                              active === item.id
                                ? "bg-accent shadow-accent/20 text-white shadow-sm"
                                : "text-text-secondary hover:bg-surface hover:text-text-primary",
                            )}
                          >
                            <item.icon className="size-5 flex-shrink-0" />
                            {item.label}
                          </button>
                        </li>
                      ))}
                    </ul>
                  </div>
                );
              })}
            </nav>
          </aside>

          {/* ── Content ── */}
          <main className="min-w-0 flex-1">
            {/* Section header */}
            <div className="border-border mb-6 border-b pb-5">
              <h1 className="text-text-primary text-2xl font-bold">
                {meta.title}
              </h1>
              <p className="text-text-secondary mt-1 text-sm">
                {meta.description}
              </p>
            </div>

            {/* Section body */}
            <div className="space-y-6">
              {active === "profile" && <MyProfileSection />}
              {active === "password" && <MyPasswordSection />}
              {active === "radarr" && <RadarrSettingsSection />}
              {active === "sonarr" && <SonarrSettingsSection />}
              {active === "webhook" && <WebhookSettingsSection />}
              {active === "servers" && <ServersSection />}
              {active === "default-perms" && <DefaultPermissionsSection />}
              {active === "jobs" && <JobsSection />}
              {active === "logs" && <LogsSection />}
              {active === "appearance" && <AppearanceSection />}
              {active === "my-requests" && <MyRequestsSection />}
              {active === "manage-requests" && <ManageRequestsSection />}
              {active === "users" && <UsersSection />}
            </div>
          </main>
        </div>
      </div>
    </div>
  );
}

