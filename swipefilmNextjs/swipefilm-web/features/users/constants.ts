import { Permission } from "@/lib/permissions";

// ✅ Utilisé par DefaultPermissionsSection (droits par défaut des nouveaux
// comptes) et UsersSection (édition par utilisateur) — même liste de bits.
export const PERMISSION_BITS: {
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

export const PRESETS: {
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
