"use client";

import { Button } from "@/components/ui/Button";
import { cn } from "@/lib/utils";

// ✅ Primitives génériques utilisées par toutes les sections de Paramètres
// (Radarr, Sonarr, Webhook, Utilisateurs, Jobs, Logs...) — extraites de
// app/(app)/settings/page.tsx pour être réutilisables par les composants de
// features/*.

export type SaveState = "idle" | "saving" | "ok" | "error";
export type VerifyState = "idle" | "verifying" | "ok" | "error";

export function SectionCard({
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

export function CardBody({
  children,
  className,
}: {
  children: React.ReactNode;
  className?: string;
}) {
  return <div className={cn("px-6 py-6", className)}>{children}</div>;
}

export function CardFooter({ children }: { children: React.ReactNode }) {
  return (
    <div className="bg-surface-alt border-border flex items-center justify-end gap-3 border-t px-6 py-4">
      {children}
    </div>
  );
}

export function FieldLabel({ children }: { children: React.ReactNode }) {
  return (
    <label className="text-text-secondary mb-1.5 block text-sm font-medium">
      {children}
    </label>
  );
}

export function FieldInput({
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

export function FieldSelect({
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

// ✅ Alias — délèguent tous au même composant Button partagé
// (components/ui/Button.tsx), juste avec un variant fixé + le radius `lg`
// utilisé partout dans les settings (vs `rounded-button` par défaut ailleurs).
export function BtnPrimary({
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

export function BtnSecondary({
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

export function BtnDanger({
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

export function Spinner({ size = "sm" }: { size?: "sm" | "md" }) {
  return (
    <span
      className={cn(
        "border-accent inline-block animate-spin rounded-full border-2 border-t-transparent",
        size === "sm" ? "h-3.5 w-3.5" : "h-5 w-5",
      )}
    />
  );
}
