"use client";

import { useState, useEffect, useCallback } from "react";
import { useTheme } from "@/hooks/useTheme";
import { TopNav } from "@/components/layout/TopNav";
import { usePermission } from "@/hooks/usePermission";
import { useAuth } from "@/context/AuthContext";
import { Permission } from "@/lib/permissions";
import {
  getRadarr,
  updateRadarr,
  getRadarrQualityProfiles,
  getRadarrRootFolders,
  getSonarr,
  updateSonarr,
  getSonarrQualityProfiles,
  getSonarrRootFolders,
  getServerConfig,
  configureServer,
  syncServer,
  getDefaultPermissions,
  updateDefaultPermissions,
  getUsers,
  updateUserPermissions,
  applyPermissionPreset,
  deleteUser,
  getUserQuota,
  updateUserQuota,
  updateUser,
  getMyProfile,
  updateMyProfile,
  changePassword,
  getRequests,
  approveRequest,
  declineRequest,
  cancelRequest,
  statusFromCode,
  getJobs,
  runJob,
  getLogs,
  type ArrConfig,
  type ArrProfile,
  type ArrRootFolder,
  type ArrPreferences,
  type UserListItem,
  type UserProfile,
  type UserQuota,
  type MovieRequest,
  type JobDefinition,
  type LogEntry,
  normalizeServer,
} from "@/lib/auth";
import { MovieRequestCard } from "@/components/movie/MovieRequestCard";
import { Button } from "@/components/ui/Button";
import { LogLevelBadge } from "@/components/ui/Badges";
import { ConnectForm } from "@/components/onboarding/ConnectForm";
import type { UserServer } from "@/types";
import { cn } from "@/lib/utils";

// ── Permission labels ────────────────────────────────────────────
const PERMISSION_BITS: {
  value: Permission;
  label: string;
  description: string;
}[] = [
  {
    value: Permission.CanSwipe,
    label: "Swipe",
    description: "Accès aux recommandations et au swipe",
  },
  {
    value: Permission.CanRequest,
    label: "Requêtes",
    description: "Ajouter des films à Radarr/Sonarr",
  },
  {
    value: Permission.AutoApprove,
    label: "Auto-approbation",
    description: "Ses requêtes sont approuvées automatiquement",
  },
  {
    value: Permission.ViewRequests,
    label: "Voir les requêtes",
    description: "Consulter toutes les requêtes",
  },
  {
    value: Permission.ManageRequests,
    label: "Gérer les requêtes",
    description: "Approuver ou refuser les requêtes",
  },
  {
    value: Permission.ViewOthersHistory,
    label: "Historique",
    description: "Voir l'activité des autres utilisateurs",
  },
  {
    value: Permission.ViewAdminDashboard,
    label: "Dashboard admin",
    description: "Accès au tableau de bord administrateur",
  },
  {
    value: Permission.ManageUsers,
    label: "Utilisateurs",
    description: "Créer, modifier et supprimer des comptes",
  },
  {
    value: Permission.Admin,
    label: "Administrateur",
    description: "Accès total sans restriction",
  },
];

const PRESETS: {
  value: "none" | "user" | "moderator" | "admin";
  label: string;
  color: string;
}[] = [
  {
    value: "none",
    label: "Aucun accès",
    color: "border-border text-text-secondary",
  },
  {
    value: "user",
    label: "Utilisateur",
    color: "border-blue-500/30 text-blue-400",
  },
  {
    value: "moderator",
    label: "Modérateur",
    color: "border-yellow-500/30 text-yellow-400",
  },
  { value: "admin", label: "Admin", color: "border-accent/30 text-accent" },
];

// ── Nav items ─────────────────────────────────────────────────────
type SectionId =
  | "profile"
  | "password"
  | "appearance"
  | "radarr"
  | "sonarr"
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
  icon: string;
  description: string;
  permission?: Permission;
  adminOnly?: boolean;
  group: string;
}

const NAV_ITEMS: NavItem[] = [
  {
    id: "profile",
    label: "Mon profil",
    icon: "ph-user-circle",
    description: "Nom affiché et adresse email",
    group: "Compte",
  },
  {
    id: "password",
    label: "Mot de passe",
    icon: "ph-lock-key",
    description: "Modifier votre mot de passe",
    group: "Compte",
  },
  {
    id: "appearance",
    label: "Apparence",
    icon: "ph-paint-brush",
    description: "Thème clair ou sombre",
    group: "Compte",
  },
  {
    id: "radarr",
    label: "Radarr",
    icon: "ph-film-clapperboard",
    description: "Téléchargement automatique de films",
    permission: Permission.CanSwipe,
    group: "Téléchargement",
  },
  {
    id: "sonarr",
    label: "Sonarr",
    icon: "ph-television-simple",
    description: "Téléchargement automatique de séries",
    permission: Permission.CanSwipe,
    group: "Téléchargement",
  },
  {
    id: "servers",
    label: "Serveurs média",
    icon: "ph-hard-drives",
    description: "Plex et Jellyfin connectés",
    adminOnly: true,
    group: "Administration",
  },
  {
    id: "default-perms",
    label: "Permissions",
    icon: "ph-shield-check",
    description: "Droits par défaut des nouveaux utilisateurs",
    adminOnly: true,
    group: "Administration",
  },
  {
    id: "jobs",
    label: "Tâches planifiées",
    icon: "ph-gear-six",
    description: "Déclencher manuellement les jobs Hangfire",
    adminOnly: true,
    group: "Administration",
  },
  {
    id: "logs",
    label: "Logs",
    icon: "ph-terminal-window",
    description: "Journal applicatif — niveau, recherche, pagination",
    adminOnly: true,
    group: "Administration",
  },
  {
    id: "users",
    label: "Utilisateurs",
    icon: "ph-users-three",
    description: "Gérer les comptes et leurs droits",
    permission: Permission.ManageUsers,
    group: "Administration",
  },
  {
    id: "my-requests",
    label: "Mes requêtes",
    icon: "ph-stack",
    description: "Films que vous avez demandés",
    permission: Permission.CanRequest,
    group: "Requêtes",
  },
  {
    id: "manage-requests",
    label: "Gérer les requêtes",
    icon: "ph-check-square",
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
              <i
                className={cn(
                  "ph-thin text-accent text-base",
                  visibleItems.find((i) => i.id === active)?.icon,
                )}
              />
              {visibleItems.find((i) => i.id === active)?.label}
            </span>
            <i
              className={cn(
                "ph-thin ph-caret-down text-text-secondary transition-transform",
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
                  <i
                    className={cn("ph-thin flex-shrink-0 text-base", item.icon)}
                  />
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
                            <i
                              className={cn(
                                "ph-thin flex-shrink-0 text-lg",
                                item.icon,
                              )}
                            />
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
              {active === "radarr" && (
                <ArrSection
                  label="Radarr"
                  description="Téléchargement automatique de films"
                  icon="ph-film-clapperboard"
                  urlPlaceholder="http://localhost:7878"
                  onGet={async () => (await getRadarr()) as any}
                  onUpdate={updateRadarr}
                  onGetProfiles={getRadarrQualityProfiles}
                  onGetFolders={getRadarrRootFolders}
                />
              )}
              {active === "sonarr" && (
                <ArrSection
                  label="Sonarr"
                  description="Téléchargement automatique de séries"
                  icon="ph-television-simple"
                  urlPlaceholder="http://localhost:8989"
                  onGet={async () => (await getSonarr()) as any}
                  onUpdate={updateSonarr}
                  onGetProfiles={getSonarrQualityProfiles}
                  onGetFolders={getSonarrRootFolders}
                />
              )}
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

// ── Shell ─────────────────────────────────────────────────────────

function SectionCard({
  children,
  className,
}: {
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <div
      className={cn(
        "border-border bg-surface overflow-hidden rounded-xl border",
        className,
      )}
    >
      {children}
    </div>
  );
}

function CardBody({
  children,
  className,
}: {
  children: React.ReactNode;
  className?: string;
}) {
  return <div className={cn("px-6 py-6", className)}>{children}</div>;
}

function CardFooter({ children }: { children: React.ReactNode }) {
  return (
    <div className="bg-surface-alt border-border flex items-center justify-end gap-3 border-t px-6 py-4">
      {children}
    </div>
  );
}

function FieldLabel({ children }: { children: React.ReactNode }) {
  return (
    <label className="text-text-secondary mb-1.5 block text-sm font-medium">
      {children}
    </label>
  );
}

function FieldInput({
  className,
  ...props
}: React.InputHTMLAttributes<HTMLInputElement>) {
  return (
    <input
      {...props}
      className={cn(
        "border-border bg-bg-primary text-text-primary w-full rounded-lg border px-3 py-2.5 text-sm",
        "placeholder:text-text-secondary/40 focus:ring-accent/40 focus:border-accent/60 transition-all focus:ring-2 focus:outline-none",
        className,
      )}
    />
  );
}

function FieldSelect({
  className,
  ...props
}: React.SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <select
      {...props}
      className={cn(
        "border-border bg-bg-primary text-text-primary w-full rounded-lg border px-3 py-2.5 text-sm",
        "focus:ring-accent/40 focus:border-accent/60 transition-all focus:ring-2 focus:outline-none",
        className,
      )}
    />
  );
}

// ✅ Alias locaux — délèguent tous au même composant Button partagé
// (components/ui/Button.tsx), juste avec un variant fixé + le radius `lg`
// utilisé partout dans les settings (vs `rounded-button` par défaut ailleurs).
function BtnPrimary({
  children,
  className,
  ...props
}: React.ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <Button
      variant="primary"
      size="sm"
      className={cn(
        "bg-accent hover:bg-accent/90 rounded-lg hover:brightness-100",
        className,
      )}
      {...props}
    >
      {children}
    </Button>
  );
}

function BtnSecondary({
  children,
  className,
  ...props
}: React.ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <Button
      variant="secondary"
      size="sm"
      className={cn("rounded-lg", className)}
      {...props}
    >
      {children}
    </Button>
  );
}

function BtnDanger({
  children,
  className,
  ...props
}: React.ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <Button
      variant="danger"
      size="sm"
      className={cn("rounded-lg", className)}
      {...props}
    >
      {children}
    </Button>
  );
}

function Spinner({ size = "sm" }: { size?: "sm" | "md" }) {
  return (
    <span
      className={cn(
        "border-accent inline-block animate-spin rounded-full border-2 border-t-transparent",
        size === "sm" ? "h-3.5 w-3.5" : "h-5 w-5",
      )}
    />
  );
}

function PermToggle({
  label,
  description,
  checked,
  onChange,
}: {
  label: string;
  description: string;
  checked: boolean;
  onChange: () => void;
}) {
  return (
    <div
      onClick={onChange}
      className="border-border group flex cursor-pointer items-center justify-between gap-4 border-b py-3 last:border-0"
    >
      <div>
        <p className="text-text-primary group-hover:text-accent text-sm font-medium transition-colors">
          {label}
        </p>
        <p className="text-text-secondary mt-0.5 text-xs">{description}</p>
      </div>
      <div
        className={cn(
          "relative h-6 w-10 flex-shrink-0 rounded-full transition-colors",
          checked ? "bg-accent" : "bg-surface-alt border-border border",
        )}
      >
        <span
          className={cn(
            "absolute top-1 h-4 w-4 rounded-full bg-white shadow transition-transform",
            checked ? "translate-x-5" : "translate-x-1",
          )}
        />
      </div>
    </div>
  );
}

// ── AppearanceSection ─────────────────────────────────────────────

function AppearanceSection() {
  const { theme, setTheme } = useTheme();

  const options: {
    value: "dark" | "light";
    label: string;
    icon: string;
    description: string;
  }[] = [
    {
      value: "dark",
      label: "Sombre",
      icon: "ph-moon",
      description: "Interface noire, idéale en soirée",
    },
    {
      value: "light",
      label: "Clair",
      icon: "ph-sun",
      description: "Interface claire pour la journée",
    },
  ];

  return (
    <SectionCard>
      <CardBody>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          {options.map((opt) => {
            const isActive = theme === opt.value;
            return (
              <button
                key={opt.value}
                onClick={() => setTheme(opt.value)}
                className={cn(
                  "relative flex cursor-pointer flex-col items-start gap-3 rounded-xl border-2 p-4 text-left transition-all",
                  isActive
                    ? "border-accent bg-accent/10 shadow-accent/20 shadow-sm"
                    : "border-border bg-surface-alt hover:border-border hover:bg-surface",
                )}
              >
                {/* Preview thumbnail */}
                <div
                  className={cn(
                    "flex h-20 w-full flex-col overflow-hidden rounded-lg border",
                    opt.value === "dark"
                      ? "border-white/10 bg-[#0A130D]"
                      : "border-black/10 bg-[#F5F0E8]",
                  )}
                >
                  {/* Fake topbar */}
                  <div
                    className={cn(
                      "flex h-5 items-center gap-1.5 px-3",
                      opt.value === "dark" ? "bg-[#111E14]" : "bg-[#EDE8DC]",
                    )}
                  >
                    <span className="bg-accent/70 h-2 w-2 rounded-full" />
                    <span
                      className={cn(
                        "h-1.5 w-16 rounded-full",
                        opt.value === "dark" ? "bg-white/10" : "bg-black/10",
                      )}
                    />
                  </div>
                  {/* Fake content */}
                  <div className="flex flex-1 flex-col gap-1.5 px-3 pt-2">
                    <span
                      className={cn(
                        "h-2 w-3/4 rounded-full",
                        opt.value === "dark" ? "bg-white/15" : "bg-black/15",
                      )}
                    />
                    <span
                      className={cn(
                        "h-1.5 w-1/2 rounded-full",
                        opt.value === "dark" ? "bg-white/8" : "bg-black/8",
                      )}
                    />
                  </div>
                </div>

                <div className="flex w-full items-start justify-between gap-2">
                  <div>
                    <div className="flex items-center gap-2">
                      <i
                        className={cn(
                          "ph-thin text-lg",
                          opt.icon,
                          isActive ? "text-accent" : "text-text-secondary",
                        )}
                      />
                      <span
                        className={cn(
                          "text-sm font-semibold",
                          isActive ? "text-accent" : "text-text-primary",
                        )}
                      >
                        {opt.label}
                      </span>
                    </div>
                    <p className="text-text-secondary mt-0.5 text-xs">
                      {opt.description}
                    </p>
                  </div>
                  {/* Checkmark */}
                  <span
                    className={cn(
                      "mt-0.5 flex h-5 w-5 flex-shrink-0 items-center justify-center rounded-full border-2 transition-all",
                      isActive ? "border-accent bg-accent" : "border-border",
                    )}
                  >
                    {isActive && (
                      <i className="ph-thin ph-check text-[10px] text-white" />
                    )}
                  </span>
                </div>
              </button>
            );
          })}
        </div>
      </CardBody>
    </SectionCard>
  );
}

// ── MyProfileSection ─────────────────────────────────────────────

function MyProfileSection() {
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [loading, setLoading] = useState(true);
  const [displayName, setDisplayName] = useState("");
  const [email, setEmail] = useState("");
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getMyProfile()
      .then((p) => {
        setProfile(p);
        setDisplayName(p.displayName);
        setEmail(p.email);
      })
      .finally(() => setLoading(false));
  }, []);

  async function handleSave() {
    setSaving(true);
    setError(null);
    try {
      const updated = await updateMyProfile({ displayName, email });
      setProfile(updated);
      setSaved(true);
      setTimeout(() => setSaved(false), 2000);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Erreur");
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
        {/* Avatar + identité */}
        <div className="border-border mb-6 flex items-center gap-4 border-b pb-6">
          <div className="bg-accent/10 border-accent/30 flex h-16 w-16 flex-shrink-0 items-center justify-center rounded-full border-2">
            <span className="text-accent text-2xl font-bold">
              {(profile?.displayName ?? "?")[0].toUpperCase()}
            </span>
          </div>
          <div>
            <p className="text-text-primary text-base font-semibold">
              {profile?.displayName}
            </p>
            <p className="text-text-secondary text-sm">{profile?.email}</p>
          </div>
        </div>

        <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
          <div>
            <FieldLabel>Nom affiché</FieldLabel>
            <FieldInput
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
            />
          </div>
          <div>
            <FieldLabel>Adresse email</FieldLabel>
            <FieldInput
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
          </div>
        </div>
        {error && <p className="mt-3 text-sm text-red-400">{error}</p>}
      </CardBody>
      <CardFooter>
        {saved && (
          <span className="mr-auto flex items-center gap-1.5 text-sm text-green-400">
            <i className="ph-thin ph-check-circle" /> Enregistré
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

// ── MyPasswordSection ─────────────────────────────────────────────

function MyPasswordSection() {
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
              <i className="ph-thin ph-check-circle" /> Modifié
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

// ── ArrSection ───────────────────────────────────────────────────

interface ArrSectionProps {
  label: string;
  description: string;
  icon: string;
  urlPlaceholder: string;
  onGet: () => Promise<ArrConfig | null>;
  onUpdate: (
    url: string,
    apiKey: string,
    prefs?: Partial<ArrPreferences>,
  ) => Promise<{ message: string; testConnectionOk: boolean }>;
  onGetProfiles: () => Promise<ArrProfile[]>;
  onGetFolders: () => Promise<ArrRootFolder[]>;
}

type SaveState = "idle" | "saving" | "ok" | "error";

function ArrSection({
  label,
  description,
  icon,
  urlPlaceholder,
  onGet,
  onUpdate,
  onGetProfiles,
  onGetFolders,
}: ArrSectionProps) {
  const [config, setConfig] = useState<ArrConfig | null>(null);
  const [loading, setLoading] = useState(true);
  const [url, setUrl] = useState("");
  const [apiKey, setApiKey] = useState("");
  const [saveState, setSaveState] = useState<SaveState>("idle");
  const [saveError, setSaveError] = useState<string | null>(null);
  const [profiles, setProfiles] = useState<ArrProfile[]>([]);
  const [folders, setFolders] = useState<ArrRootFolder[]>([]);
  const [loadingPrefs, setLoadingPrefs] = useState(false);
  const [selectedProfileId, setSelectedProfileId] = useState<number | null>(
    null,
  );
  const [selectedFolder, setSelectedFolder] = useState("");

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const cfg = await onGet();
      setConfig(cfg);
      if (cfg) {
        setUrl(cfg.url ?? "");
        // Ne pas pré-remplir la clé — on affiche seulement le hint
        if (cfg.isConfigured) {
          setSelectedProfileId(cfg.defaultQualityProfileId ?? null);
          setSelectedFolder(cfg.defaultRootFolderPath ?? "");
          // Charger les profils / dossiers si déjà configuré
          setLoadingPrefs(true);
          Promise.all([onGetProfiles(), onGetFolders()])
            .then(([p, f]) => {
              setProfiles(p);
              setFolders(f);
              if (p.length > 0 && !cfg.defaultQualityProfileId)
                setSelectedProfileId(p[0].id);
              if (f.length > 0 && !cfg.defaultRootFolderPath)
                setSelectedFolder(f[0].path);
            })
            .finally(() => setLoadingPrefs(false));
        }
      }
    } finally {
      setLoading(false);
    }
  }, [onGet, onGetProfiles, onGetFolders]);

  useEffect(() => {
    load();
  }, [load]);

  async function handleSave() {
    if (!url || !apiKey) {
      setSaveError("URL et clé API requis.");
      return;
    }
    setSaveState("saving");
    setSaveError(null);
    try {
      const prefs: Partial<ArrPreferences> = {};
      if (selectedProfileId !== null)
        prefs.defaultQualityProfileId = selectedProfileId;
      if (selectedFolder) prefs.defaultRootFolderPath = selectedFolder;
      const res = await onUpdate(url, apiKey, prefs);
      if (!res.testConnectionOk)
        throw new Error("Connexion échouée — vérifiez l'URL et la clé API");
      setSaveState("ok");
      // Recharger les profils / dossiers avec la nouvelle config
      setLoadingPrefs(true);
      const [p, f] = await Promise.all([onGetProfiles(), onGetFolders()]);
      setProfiles(p);
      setFolders(f);
      if (p.length > 0 && selectedProfileId === null)
        setSelectedProfileId(p[0].id);
      if (f.length > 0 && !selectedFolder) setSelectedFolder(f[0].path);
      setLoadingPrefs(false);
      await load();
    } catch (e) {
      setSaveState("error");
      setSaveError(e instanceof Error ? e.message : "Erreur de connexion");
    }
  }

  const isConfigured = config?.isConfigured ?? false;
  const showPrefs = isConfigured || saveState === "ok";

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
      {/* Status header */}
      <div className="border-border flex items-center gap-4 border-b px-6 py-5">
        <div
          className={cn(
            "flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-lg",
            isConfigured ? "bg-accent/10" : "bg-surface-alt",
          )}
        >
          <i
            className={cn(
              "ph-thin text-xl",
              icon,
              isConfigured ? "text-accent" : "text-text-secondary",
            )}
          />
        </div>
        <div className="flex-1">
          <div className="flex items-center gap-2">
            <span className="text-text-primary font-semibold">{label}</span>
            <span
              className={cn(
                "rounded-full px-2 py-0.5 text-[10px] font-medium tracking-wider uppercase",
                isConfigured
                  ? "bg-green-500/15 text-green-400"
                  : "bg-surface-alt text-text-secondary/60",
              )}
            >
              {isConfigured ? "Connecté" : "Non configuré"}
            </span>
          </div>
          <p className="text-text-secondary mt-0.5 text-xs">{description}</p>
        </div>
      </div>

      <CardBody className="space-y-5">
        <div>
          <FieldLabel>URL du serveur</FieldLabel>
          <FieldInput
            type="url"
            value={url}
            onChange={(e) => {
              setUrl(e.target.value);
              setSaveState("idle");
            }}
            placeholder={urlPlaceholder}
          />
        </div>
        <div>
          <FieldLabel>
            Clé API{" "}
            {isConfigured && config?.apiKeyHint && (
              <span className="text-text-secondary/50 font-normal">
                (actuelle : {config.apiKeyHint}…)
              </span>
            )}
          </FieldLabel>
          <FieldInput
            value={apiKey}
            onChange={(e) => {
              setApiKey(e.target.value);
              setSaveState("idle");
            }}
            placeholder={
              isConfigured
                ? "Laisser vide pour conserver l'actuelle"
                : "••••••••••••••••••••••••••••••••"
            }
            className="font-mono"
          />
        </div>

        {showPrefs && (
          <div className="border-border bg-bg-primary/50 space-y-4 rounded-lg border p-4">
            <p className="text-text-secondary text-xs font-semibold tracking-wider uppercase">
              Préférences de téléchargement
            </p>
            {loadingPrefs ? (
              <div className="text-text-secondary flex items-center gap-2 text-xs">
                <Spinner />
                Chargement…
              </div>
            ) : (
              <>
                {profiles.length > 0 && (
                  <div>
                    <FieldLabel>Profil de qualité</FieldLabel>
                    <FieldSelect
                      value={selectedProfileId ?? ""}
                      onChange={(e) =>
                        setSelectedProfileId(Number(e.target.value))
                      }
                    >
                      {profiles.map((p) => (
                        <option key={p.id} value={p.id}>
                          {p.name}
                        </option>
                      ))}
                    </FieldSelect>
                  </div>
                )}
                {folders.length > 0 && (
                  <div>
                    <FieldLabel>Dossier de destination</FieldLabel>
                    <FieldSelect
                      value={selectedFolder}
                      onChange={(e) => setSelectedFolder(e.target.value)}
                    >
                      {folders.map((f) => (
                        <option key={f.path} value={f.path}>
                          {f.path}
                          {f.freeSpaceGb !== undefined
                            ? ` (${f.freeSpaceGb.toFixed(1)} Go libres)`
                            : ""}
                        </option>
                      ))}
                    </FieldSelect>
                  </div>
                )}
              </>
            )}
          </div>
        )}

        {saveState === "error" && saveError && (
          <p className="text-sm text-red-400">{saveError}</p>
        )}
        {saveState === "ok" && (
          <p className="flex items-center gap-1.5 text-sm text-green-400">
            <i className="ph-thin ph-check-circle" />
            Configuration enregistrée et connexion vérifiée.
          </p>
        )}
      </CardBody>

      <CardFooter>
        <BtnPrimary
          onClick={handleSave}
          disabled={saveState === "saving" || !url}
        >
          {saveState === "saving" ? (
            <>
              <Spinner />
              Connexion en cours…
            </>
          ) : (
            "Enregistrer"
          )}
        </BtnPrimary>
      </CardFooter>
    </SectionCard>
  );
}

// ── ServersSection ───────────────────────────────────────────────

function ServersSection() {
  const [server, setServer] = useState<UserServer | null>(null);
  const [loading, setLoading] = useState(true);
  const [syncing, setSyncing] = useState(false);
  const [showForm, setShowForm] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const s = await getServerConfig();
      setServer(s ? normalizeServer(s) : null);
    } catch {
      /* silencieux */
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function handleSync() {
    setSyncing(true);
    setError(null);
    try {
      await syncServer();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Erreur");
    } finally {
      setSyncing(false);
    }
  }

  async function handleConfigure(data: {
    type: string;
    friendlyName: string;
    url: string;
    apiKey?: string;
    username?: string;
    password?: string;
  }) {
    await configureServer({
      friendlyName: data.friendlyName,
      type: data.type === "jellyfin" ? "jellyfin" : "plex",
      apiKey: data.apiKey,
      username: data.username,
      password: data.password,
      url: data.url,
    });
    setShowForm(false);
    await load();
  }

  if (loading)
    return (
      <SectionCard>
        <CardBody>
          <Spinner size="md" />
        </CardBody>
      </SectionCard>
    );

  // ✅ Un seul serveur pour toute l'instance — formulaire de (re)configuration
  // à la place de la liste + bouton "ajouter" d'avant.
  if (!server || showForm) {
    return (
      <SectionCard>
        <CardBody>
          <ConnectForm
            title={
              server ? "Reconfigurer le serveur" : "Connecter un serveur média"
            }
            options={[
              { id: "jellyfin", label: "Jellyfin" },
              { id: "plex", label: "Plex" },
            ]}
            onSubmit={handleConfigure}
            onBack={server ? () => setShowForm(false) : undefined}
          />
        </CardBody>
      </SectionCard>
    );
  }

  const typeLabel =
    server.type === 1 || server.type === "jellyfin" ? "Jellyfin" : "Plex";

  return (
    <SectionCard>
      {error && (
        <div className="px-6 pt-4">
          <p className="text-sm text-red-400">{error}</p>
        </div>
      )}
      <div className="flex items-center gap-4 px-6 py-4">
        <div className="bg-accent/10 border-accent/20 flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-lg border">
          <i className="ph-thin ph-broadcast text-accent text-base" />
        </div>
        <div className="min-w-0 flex-1">
          <p className="text-text-primary truncate text-sm font-semibold">
            {server.friendlyName ?? typeLabel}
          </p>
          <p className="text-text-secondary text-xs">
            {typeLabel}
            {server.lastSyncAt &&
              ` · Sync ${new Date(server.lastSyncAt).toLocaleString()}`}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <BtnSecondary onClick={handleSync} disabled={syncing}>
            {syncing ? (
              <Spinner />
            ) : (
              <i className="ph-thin ph-arrows-clockwise text-sm" />
            )}
            Sync
          </BtnSecondary>
          <BtnSecondary onClick={() => setShowForm(true)}>
            <i className="ph-thin ph-pencil-simple text-sm" />
            Reconfigurer
          </BtnSecondary>
        </div>
      </div>
    </SectionCard>
  );
}

// ── DefaultPermissionsSection ────────────────────────────────────

function DefaultPermissionsSection() {
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
            <i className="ph-thin ph-check-circle" /> Enregistré
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

// ── JobsSection ───────────────────────────────────────────────────

function JobsSection() {
  const [jobs, setJobs] = useState<JobDefinition[]>([]);
  const [loading, setLoading] = useState(true);
  const [running, setRunning] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<Record<string, string>>({});

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setJobs(await getJobs());
    } catch {
      /* silencieux */
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function handleRun(id: string) {
    setRunning(id);
    setFeedback((f) => ({ ...f, [id]: "" }));
    try {
      await runJob(id);
      setFeedback((f) => ({ ...f, [id]: "Job lancé ⏳" }));
    } catch (e) {
      setFeedback((f) => ({
        ...f,
        [id]: e instanceof Error ? e.message : "Erreur",
      }));
    } finally {
      setRunning(null);
    }
  }

  function formatDate(iso: string | null) {
    if (!iso) return "jamais";
    return new Date(iso).toLocaleString("fr-FR", {
      dateStyle: "short",
      timeStyle: "short",
    });
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
      {jobs.length === 0 ? (
        <CardBody>
          <div className="text-text-secondary flex flex-col items-center justify-center gap-3 py-8">
            <i className="ph-thin ph-gear-six text-4xl" />
            <p className="text-sm">Aucun job enregistré.</p>
          </div>
        </CardBody>
      ) : (
        <ul className="divide-border divide-y">
          {jobs.map((job) => (
            <li key={job.id} className="flex items-center gap-4 px-6 py-4">
              <div className="bg-accent/10 border-accent/20 flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-lg border">
                <i className="ph-thin ph-gear-six text-accent text-base" />
              </div>
              <div className="min-w-0 flex-1">
                <p className="text-text-primary truncate text-sm font-semibold">
                  {job.label}
                </p>
                <p className="text-text-secondary text-xs">{job.description}</p>
                <p className="text-text-secondary/70 mt-1 text-[11px]">
                  Dernière exécution : {formatDate(job.lastExecution)} ·
                  Prochaine : {formatDate(job.nextExecution)}
                </p>
                {feedback[job.id] && (
                  <p className="text-accent mt-1 text-xs">{feedback[job.id]}</p>
                )}
              </div>
              <BtnSecondary
                onClick={() => handleRun(job.id)}
                disabled={running === job.id}
              >
                {running === job.id ? (
                  <Spinner />
                ) : (
                  <i className="ph-thin ph-play text-sm" />
                )}
                Lancer
              </BtnSecondary>
            </li>
          ))}
        </ul>
      )}
    </SectionCard>
  );
}

// ── LogsSection ───────────────────────────────────────────────────

const LOG_LEVELS = [
  "Verbose",
  "Debug",
  "Information",
  "Warning",
  "Error",
  "Fatal",
];

function LogsSection() {
  const [entries, setEntries] = useState<LogEntry[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [level, setLevel] = useState("");
  const [search, setSearch] = useState("");
  const [searchInput, setSearchInput] = useState("");
  const [loading, setLoading] = useState(true);
  const pageSize = 25;
  const totalPages = Math.max(1, Math.ceil(total / pageSize));

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await getLogs({
        page,
        pageSize,
        level: level || undefined,
        search: search || undefined,
      });
      setEntries(res.results);
      setTotal(res.total);
    } catch {
      /* silencieux */
    } finally {
      setLoading(false);
    }
  }, [page, level, search]);

  useEffect(() => {
    load();
  }, [load]);

  function handleSearchSubmit(e: React.FormEvent) {
    e.preventDefault();
    setPage(1);
    setSearch(searchInput.trim());
  }

  return (
    <SectionCard>
      <div className="border-border flex flex-wrap items-center gap-2 border-b px-6 py-4">
        <select
          value={level}
          onChange={(e) => {
            setLevel(e.target.value);
            setPage(1);
          }}
          className="rounded-input border-border bg-surface text-text-primary border px-2 py-1.5 text-sm"
        >
          <option value="">Tous les niveaux</option>
          {LOG_LEVELS.map((l) => (
            <option key={l} value={l}>
              {l}
            </option>
          ))}
        </select>

        <form
          onSubmit={handleSearchSubmit}
          className="flex min-w-[200px] flex-1 gap-2"
        >
          <input
            type="text"
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            placeholder="Rechercher dans les messages…"
            className="rounded-input border-border bg-surface text-text-primary placeholder:text-text-secondary/60 flex-1 border px-3 py-1.5 text-sm"
          />
          <BtnSecondary type="submit">Rechercher</BtnSecondary>
        </form>
      </div>

      {loading ? (
        <CardBody>
          <Spinner size="md" />
        </CardBody>
      ) : entries.length === 0 ? (
        <CardBody>
          <div className="text-text-secondary flex flex-col items-center justify-center gap-3 py-8">
            <i className="ph-thin ph-terminal-window text-4xl" />
            <p className="text-sm">Aucune entrée pour ce filtre.</p>
          </div>
        </CardBody>
      ) : (
        <ul className="divide-border divide-y">
          {entries.map((entry, i) => (
            <li key={i} className="flex items-start gap-3 px-6 py-3">
              <LogLevelBadge level={entry.level} />
              <div className="min-w-0 flex-1">
                <p className="text-text-primary text-sm break-words">
                  {entry.message}
                </p>
                <p className="text-text-secondary/70 mt-0.5 text-[11px]">
                  {new Date(entry.timestamp).toLocaleString("fr-FR")}
                  {entry.sourceContext && ` · ${entry.sourceContext}`}
                </p>
              </div>
            </li>
          ))}
        </ul>
      )}

      <CardFooter>
        <span className="text-text-secondary mr-auto text-xs">
          {total} entrée{total > 1 ? "s" : ""} · page {page}/{totalPages}
        </span>
        <BtnSecondary
          onClick={() => setPage((p) => Math.max(1, p - 1))}
          disabled={page <= 1}
        >
          Précédent
        </BtnSecondary>
        <BtnSecondary
          onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
          disabled={page >= totalPages}
        >
          Suivant
        </BtnSecondary>
      </CardFooter>
    </SectionCard>
  );
}

// ── MyRequestsSection ─────────────────────────────────────────────

function MyRequestsSection() {
  const [requests, setRequests] = useState<MovieRequest[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRequests(await getRequests());
    } catch {
      /* silencieux */
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function handleCancel(id: string) {
    try {
      await cancelRequest(id);
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Erreur");
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
    <div className="space-y-4">
      {error && <p className="text-sm text-red-400">{error}</p>}
      {requests.length === 0 ? (
        <SectionCard>
          <CardBody>
            <div className="text-text-secondary flex flex-col items-center justify-center gap-3 py-8">
              <i className="ph-thin ph-stack text-4xl" />
              <p className="text-sm">Aucune requête pour l&apos;instant.</p>
            </div>
          </CardBody>
        </SectionCard>
      ) : (
        requests.map((req) => (
          <MovieRequestCard
            key={req.id}
            request={req}
            onCancel={handleCancel}
          />
        ))
      )}
    </div>
  );
}

// ── ManageRequestsSection ─────────────────────────────────────────

function ManageRequestsSection() {
  const [requests, setRequests] = useState<MovieRequest[]>([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState<0 | null>(0); // 0=pending, null=all
  const [decliningId, setDecliningId] = useState<string | null>(null);
  const [declineReason, setDeclineReason] = useState("");
  const [actioning, setActioning] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRequests(await getRequests());
    } catch {
      /* silencieux */
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function handleApprove(id: string) {
    setActioning(id);
    try {
      await approveRequest(id);
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Erreur");
    } finally {
      setActioning(null);
    }
  }

  async function handleDeclineSubmit(id: string) {
    if (!declineReason.trim()) return;
    setActioning(id);
    try {
      await declineRequest(id, declineReason);
      setDecliningId(null);
      setDeclineReason("");
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Erreur");
    } finally {
      setActioning(null);
    }
  }

  const filtered =
    filter === 0 ? requests.filter((r) => r.status === 0) : requests;

  if (loading)
    return (
      <SectionCard>
        <CardBody>
          <Spinner size="md" />
        </CardBody>
      </SectionCard>
    );

  return (
    <div className="space-y-4">
      {/* Filtre */}
      <div className="flex gap-1.5">
        {[
          { label: "En attente", value: 0 as const },
          { label: "Toutes", value: null as null },
        ].map((f) => (
          <button
            key={String(f.value)}
            onClick={() => setFilter(f.value)}
            className={cn(
              "cursor-pointer rounded-lg border px-4 py-2 text-sm font-medium transition-colors",
              filter === f.value
                ? "bg-accent/10 border-accent/30 text-accent"
                : "border-border text-text-secondary hover:text-text-primary",
            )}
          >
            {f.label}
            {f.value === 0 &&
              requests.filter((r) => r.status === 0).length > 0 && (
                <span className="bg-accent ml-2 rounded-full px-1.5 py-0.5 text-[10px] text-white">
                  {requests.filter((r) => r.status === 0).length}
                </span>
              )}
          </button>
        ))}
      </div>

      {error && <p className="text-sm text-red-400">{error}</p>}

      {filtered.length === 0 ? (
        <SectionCard>
          <CardBody>
            <div className="text-text-secondary flex flex-col items-center justify-center gap-3 py-8">
              <i className="ph-thin ph-check-square text-4xl" />
              <p className="text-sm">
                Aucune requête{filter === 0 ? " en attente" : ""}.
              </p>
            </div>
          </CardBody>
        </SectionCard>
      ) : (
        filtered.map((req) => (
          <div key={req.id} className="space-y-2">
            <MovieRequestCard
              request={req}
              isManager
              onApprove={handleApprove}
              onDecline={(id) => {
                setDecliningId(id);
                setDeclineReason("");
              }}
            />
            {decliningId === req.id && (
              <SectionCard>
                <CardBody className="py-4">
                  <p className="text-text-primary mb-3 text-sm font-medium">
                    Raison du refus
                  </p>
                  <div className="flex gap-2">
                    <FieldInput
                      value={declineReason}
                      onChange={(e) => setDeclineReason(e.target.value)}
                      placeholder="Ex : déjà disponible, hors catalogue…"
                      className="flex-1"
                    />
                    <BtnDanger
                      onClick={() => handleDeclineSubmit(req.id)}
                      disabled={!declineReason.trim() || actioning === req.id}
                    >
                      {actioning === req.id ? <Spinner /> : "Refuser"}
                    </BtnDanger>
                    <BtnSecondary onClick={() => setDecliningId(null)}>
                      Annuler
                    </BtnSecondary>
                  </div>
                </CardBody>
              </SectionCard>
            )}
          </div>
        ))
      )}
    </div>
  );
}

// ── UsersSection ─────────────────────────────────────────────────

type UserTab = "permissions" | "profil" | "quota";

function UsersSection() {
  const [users, setUsers] = useState<UserListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [editing, setEditing] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<UserTab>("permissions");
  const [editPerms, setEditPerms] = useState(0);
  const [editDisplayName, setEditDisplayName] = useState("");
  const [editEmail, setEditEmail] = useState("");
  const [quota, setQuota] = useState<UserQuota | null>(null);
  const [editQuotaLimit, setEditQuotaLimit] = useState("");
  const [saving, setSaving] = useState(false);
  const [deleting, setDeleting] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setUsers(await getUsers());
    } catch {
      /* silencieux */
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function startEdit(user: UserListItem) {
    setEditing(user.id);
    setActiveTab("permissions");
    setEditPerms(user.permissions);
    setEditDisplayName(user.displayName);
    setEditEmail(user.email);
    setError(null);
    setQuota(null);
    try {
      const q = await getUserQuota(user.id);
      setQuota(q);
      setEditQuotaLimit(String(q.limit));
    } catch {
      /* quota optionnel */
    }
  }

  async function handleApplyPreset(
    userId: string,
    preset: "none" | "user" | "moderator" | "admin",
  ) {
    setSaving(true);
    try {
      await applyPermissionPreset(userId, preset);
      await load();
      setEditing(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Erreur");
    } finally {
      setSaving(false);
    }
  }

  async function handleSavePerms(userId: string) {
    setSaving(true);
    try {
      await updateUserPermissions(userId, editPerms);
      await load();
      setEditing(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Erreur");
    } finally {
      setSaving(false);
    }
  }

  async function handleSaveProfil(userId: string) {
    setSaving(true);
    try {
      await updateUser(userId, {
        displayName: editDisplayName,
        email: editEmail,
      });
      await load();
      setEditing(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Erreur");
    } finally {
      setSaving(false);
    }
  }

  async function handleSaveQuota(userId: string) {
    setSaving(true);
    try {
      const updated = await updateUserQuota(userId, {
        limit: parseInt(editQuotaLimit, 10),
      });
      setQuota(updated);
      setEditing(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Erreur");
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(userId: string) {
    if (!confirm("Supprimer cet utilisateur ?")) return;
    setDeleting(userId);
    try {
      await deleteUser(userId);
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Erreur");
    } finally {
      setDeleting(null);
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
    <div className="space-y-3">
      {error && <p className="text-sm text-red-400">{error}</p>}

      {users.map((user) => {
        const isEditingThis = editing === user.id;
        const userIsAdmin = (user.permissions & Permission.Admin) !== 0;

        return (
          <SectionCard key={user.id}>
            {/* User row */}
            <div className="flex items-center gap-4 px-6 py-4">
              <div className="bg-surface-alt border-border flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-full border">
                <span className="text-text-secondary text-sm font-bold">
                  {user.displayName[0]?.toUpperCase()}
                </span>
              </div>
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-2">
                  <p className="text-text-primary truncate text-sm font-semibold">
                    {user.displayName}
                  </p>
                  {userIsAdmin && (
                    <span className="bg-accent/10 text-accent flex-shrink-0 rounded-full px-2 py-0.5 text-[10px] font-medium tracking-wider uppercase">
                      Admin
                    </span>
                  )}
                </div>
                <p className="text-text-secondary truncate text-xs">
                  {user.email}
                </p>
              </div>
              <div className="flex items-center gap-2">
                <BtnSecondary
                  onClick={() =>
                    isEditingThis ? setEditing(null) : startEdit(user)
                  }
                >
                  {isEditingThis ? "Fermer" : "Modifier"}
                </BtnSecondary>
                <BtnDanger
                  onClick={() => handleDelete(user.id)}
                  disabled={deleting === user.id}
                >
                  {deleting === user.id ? (
                    <Spinner />
                  ) : (
                    <i className="ph-thin ph-trash text-sm" />
                  )}
                </BtnDanger>
              </div>
            </div>

            {/* Edit panel */}
            {isEditingThis && (
              <div className="border-border border-t">
                {/* Tabs */}
                <div className="border-border flex border-b px-6">
                  {(["permissions", "profil", "quota"] as UserTab[]).map(
                    (tab) => (
                      <button
                        key={tab}
                        onClick={() => setActiveTab(tab)}
                        className={cn(
                          "-mb-px cursor-pointer border-b-2 px-4 py-3 text-sm font-medium capitalize transition-colors",
                          activeTab === tab
                            ? "border-accent text-accent"
                            : "text-text-secondary hover:text-text-primary border-transparent",
                        )}
                      >
                        {tab}
                      </button>
                    ),
                  )}
                </div>

                <CardBody className="space-y-4">
                  {/* Permissions */}
                  {activeTab === "permissions" && (
                    <>
                      <div className="flex flex-wrap gap-2">
                        {PRESETS.map((p) => (
                          <button
                            key={p.value}
                            onClick={() => handleApplyPreset(user.id, p.value)}
                            disabled={saving}
                            className={cn(
                              "cursor-pointer rounded-lg border px-3 py-1.5 text-xs transition-colors disabled:opacity-40",
                              p.color,
                              "bg-transparent hover:opacity-80",
                            )}
                          >
                            {p.label}
                          </button>
                        ))}
                      </div>
                      <div className="border-border overflow-hidden rounded-lg border">
                        {PERMISSION_BITS.map(
                          ({ value, label, description }) => (
                            <PermToggle
                              key={value}
                              label={label}
                              description={description}
                              checked={(editPerms & value) !== 0}
                              onChange={() =>
                                setEditPerms((p) =>
                                  (p & value) !== 0 ? p & ~value : p | value,
                                )
                              }
                            />
                          ),
                        )}
                      </div>
                      <div className="flex justify-end">
                        <BtnPrimary
                          onClick={() => handleSavePerms(user.id)}
                          disabled={saving}
                        >
                          {saving && <Spinner />} Enregistrer
                        </BtnPrimary>
                      </div>
                    </>
                  )}

                  {/* Profil */}
                  {activeTab === "profil" && (
                    <>
                      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                        <div>
                          <FieldLabel>Nom affiché</FieldLabel>
                          <FieldInput
                            value={editDisplayName}
                            onChange={(e) => setEditDisplayName(e.target.value)}
                          />
                        </div>
                        <div>
                          <FieldLabel>Email</FieldLabel>
                          <FieldInput
                            type="email"
                            value={editEmail}
                            onChange={(e) => setEditEmail(e.target.value)}
                          />
                        </div>
                      </div>
                      <div className="flex justify-end">
                        <BtnPrimary
                          onClick={() => handleSaveProfil(user.id)}
                          disabled={saving}
                        >
                          {saving && <Spinner />} Enregistrer
                        </BtnPrimary>
                      </div>
                    </>
                  )}

                  {/* Quota */}
                  {activeTab === "quota" && (
                    <>
                      {quota && (
                        <div className="bg-bg-primary border-border grid grid-cols-2 gap-4 rounded-lg border p-4">
                          <div>
                            <p className="text-text-secondary text-xs">
                              Utilisé
                            </p>
                            <p className="text-text-primary text-lg font-bold">
                              {quota.used}
                            </p>
                          </div>
                          {quota.resetsAt && (
                            <div>
                              <p className="text-text-secondary text-xs">
                                Réinitialisation
                              </p>
                              <p className="text-text-primary text-sm font-medium">
                                {new Date(quota.resetsAt).toLocaleDateString(
                                  "fr-FR",
                                )}
                              </p>
                            </div>
                          )}
                        </div>
                      )}
                      <div className="max-w-xs">
                        <FieldLabel>Limite de requêtes</FieldLabel>
                        <FieldInput
                          type="number"
                          min="0"
                          value={editQuotaLimit}
                          onChange={(e) => setEditQuotaLimit(e.target.value)}
                        />
                      </div>
                      <div className="flex justify-end">
                        <BtnPrimary
                          onClick={() => handleSaveQuota(user.id)}
                          disabled={saving}
                        >
                          {saving && <Spinner />} Enregistrer
                        </BtnPrimary>
                      </div>
                    </>
                  )}
                </CardBody>
              </div>
            )}
          </SectionCard>
        );
      })}
    </div>
  );
}
