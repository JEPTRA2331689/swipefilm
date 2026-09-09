import { cva, type VariantProps } from "class-variance-authority";
import {
  Clock,
  Check,
  CircleArrowDown,
  CircleDashed,
  CircleCheck,
  XCircle,
  MinusCircle,
  type LucideIcon,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { statusFromCode, type MovieRequest } from "@/features/catalog/api";

// ✅ Dot de disponibilité — un seul composant, une seule convention de
// couleur (vert=dispo, jaune=non dispo) réutilisée partout (posters,
// carrousel, cartes de saison) au lieu d'être dupliquée à chaque endroit.
export function AvailabilityDot({
  isAvailable,
  className,
}: {
  isAvailable: boolean;
  className?: string;
}) {
  return (
    <div
      className={cn(
        "ring-bg-primary/60 h-4 aspect-square ring-2",
        isAvailable ? "bg-success" : "bg-swipe-star",
        className,
      )}
      role="img"
      aria-label={isAvailable ? "Disponible" : "Non disponible"}
      title={isAvailable ? "Disponible" : "Non disponible"}
    />
  );
}

const scoreBadgeVariants = cva(
  "font-display font-bold text-text-primary drop-shadow-[0_1px_4px_rgba(0,0,0,0.6)]",
  {
    variants: {
      size: {
        sm: "text-sm",
        md: "text-lg",
        lg: "text-2xl",
      },
    },
    defaultVariants: { size: "md" },
  },
);

export function ScoreBadge({
  rating,
  size,
}: { rating: number } & VariantProps<typeof scoreBadgeVariants>) {
  return (
    <span className={scoreBadgeVariants({ size })}>
      {rating.toFixed(1)}
      <span className="text-swipe-star">*</span>
    </span>
  );
}

// ── Pill générique — tags neutres (genre, certification, année, type) ──

export const pillVariants = cva(
  "rounded-pill border px-2 py-0.5 text-[11px] font-medium",
  {
    variants: {
      variant: {
        neutral: "border-border-strong bg-surface text-text-secondary",
        accent: "border-accent/20 bg-accent/10 text-accent/90",
        available: "border-swipe-star/40 bg-swipe-star/10 text-swipe-star",
        unavailable: "border-border bg-surface text-text-secondary",
      },
    },
    defaultVariants: { variant: "neutral" },
  },
);

export function Pill({
  children,
  variant,
  className,
}: { children: React.ReactNode; className?: string } & VariantProps<
  typeof pillVariants
>) {
  return (
    <span className={cn(pillVariants({ variant }), className)}>{children}</span>
  );
}

// ── Statut de requête — Pending/Approved/Downloading/PartiallyAvailable/
// Available/Declined/None — même badge partout (carte de requête, cartes
// de saison, modal de requête série).

type StatusConfig = {
  label: string;
  stripe: string; // couleur de bande latérale (MovieRequestCard)
  badge: string; // fond + texte du badge
  icon: LucideIcon;
};

export function getStatusConfig(
  statusCode: MovieRequest["status"],
  forced?: "available" | "pending" | "downloading",
): StatusConfig {
  const status = forced ?? statusFromCode(statusCode);

  switch (status) {
    case "pending":
      return {
        label: "En attente",
        stripe: "bg-yellow-500",
        badge: "bg-yellow-500/20 text-yellow-400 border-yellow-500/30",
        icon: Clock,
      };
    case "approved":
      return {
        label: "Approuvée",
        stripe: "bg-blue-400",
        badge: "bg-blue-400/20 text-blue-300 border-blue-400/30",
        icon: Check,
      };
    case "downloading":
      return {
        label: "En téléchargement",
        stripe: "bg-blue-500",
        badge: "bg-blue-500/20 text-blue-400 border-blue-500/30",
        icon: CircleArrowDown,
      };
    case "partially-available":
      return {
        label: "Partiellement disponible",
        stripe: "bg-orange-500",
        badge: "bg-orange-500/20 text-orange-400 border-orange-500/30",
        icon: CircleDashed,
      };
    case "available":
      return {
        label: "Disponible",
        stripe: "bg-green-500",
        badge: "bg-green-500/20 text-green-400 border-green-500/30",
        icon: CircleCheck,
      };
    case "declined":
      return {
        label: "Refusée",
        stripe: "bg-red-500",
        badge: "bg-red-500/20 text-red-400 border-red-500/30",
        icon: XCircle,
      };
    case "none":
    default:
      return {
        label: "Non demandé",
        stripe: "bg-surface-alt",
        badge: "bg-surface-alt text-text-secondary border-border",
        icon: MinusCircle,
      };
  }
}

export function RequestStatusBadge({
  statusCode,
  forced,
  className,
}: {
  statusCode: MovieRequest["status"];
  forced?: "available" | "pending" | "downloading";
  className?: string;
}) {
  const status = getStatusConfig(statusCode, forced);
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1.5 rounded-full border px-2.5 py-1 text-[11px] font-medium whitespace-nowrap",
        status.badge,
        className,
      )}
    >
      <status.icon className="size-4" />
      {status.label}
    </span>
  );
}

// ── Niveau de log (Serilog) — Settings → Logs ──

export function LogLevelBadge({
  level,
  className,
}: {
  level: string;
  className?: string;
}) {
  const colors: Record<string, string> = {
    Error: "border-red-500/30 bg-red-500/10 text-red-400",
    Fatal: "border-red-500/30 bg-red-500/10 text-red-400",
    Warning: "border-yellow-500/30 bg-yellow-500/10 text-yellow-400",
    Debug: "border-border bg-surface text-text-secondary",
    Verbose: "border-border bg-surface text-text-secondary",
  };

  return (
    <span
      className={cn(
        "rounded-pill flex-shrink-0 border px-2 py-0.5 text-[11px] font-medium",
        colors[level] ?? "border-accent/30 bg-accent/10 text-accent",
        className,
      )}
    >
      {level}
    </span>
  );
}
