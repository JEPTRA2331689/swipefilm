import { cva, type VariantProps } from "class-variance-authority";
import {
  type AnchorHTMLAttributes,
  type ButtonHTMLAttributes,
  forwardRef,
} from "react";
import { cn } from "@/lib/utils";

// ✅ class-variance-authority — convention standard côté composants Tailwind
// (celle de shadcn/ui) pour des variants/tailles, plutôt qu'un Record<...>
// écrit à la main. buttonVariants() est réutilisé tel quel par ButtonLink
// pour qu'un <a> stylé comme un bouton n'ait jamais à dupliquer les classes.
export const buttonVariants = cva(
  "inline-flex cursor-pointer items-center justify-center gap-2 font-body font-medium tracking-wide transition-all duration-150 disabled:pointer-events-none disabled:opacity-40",
  {
    variants: {
      variant: {
        primary:
          "bg-primary text-text-primary hover:brightness-110 active:scale-[0.98]",
        outline:
          "border border-accent bg-transparent text-accent hover:bg-accent/10 active:scale-[0.98]",
        ghost:
          "bg-transparent text-text-secondary hover:bg-surface-alt/50 hover:text-text-primary",
        // ✅ Like/Dislike pleine couleur (fiche film/série) — fond plein +
        // texte noir gras, contraste volontairement fort plutôt que teinté.
        like: "bg-swipe-like text-black hover:brightness-105 active:scale-[0.97]",
        skip: "bg-swipe-skip text-black hover:brightness-105 active:scale-[0.97]",
        // ✅ Bouton neutre bordé — settings (Sync, Annuler...), pagination
        secondary:
          "border border-border bg-surface text-text-secondary hover:bg-surface-alt/60 hover:text-text-primary active:scale-[0.98]",
        // ✅ Actions destructives — supprimer, refuser
        danger:
          "border border-red-500/30 bg-red-500/10 text-red-400 hover:bg-red-500/20 active:scale-[0.98]",
        // ✅ Accent doux — Requêter/Approuver/actions secondaires mises en avant
        "accent-soft":
          "border border-accent/40 bg-accent/10 text-accent hover:bg-accent/20 active:scale-[0.98]",
      },
      size: {
        sm: "px-4 py-2 text-sm",
        md: "px-6 py-3.5 text-[15px]",
        lg: "px-8 py-4 text-base",
      },
    },
    defaultVariants: { variant: "primary", size: "md" },
  },
);

export interface ButtonProps
  extends
    ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {}

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant, size, children, ...props }, ref) => {
    return (
      <button
        ref={ref}
        className={cn(buttonVariants({ variant, size }), className)}
        {...props}
      >
        {children}
      </button>
    );
  },
);
Button.displayName = "Button";

// ── ButtonLink — même style que Button mais rend un vrai <a> (lien externe,
// navigation) — évite d'imbriquer un <button> dans un <a> ou l'inverse ──

export interface ButtonLinkProps
  extends
    AnchorHTMLAttributes<HTMLAnchorElement>,
    VariantProps<typeof buttonVariants> {}

export const ButtonLink = forwardRef<HTMLAnchorElement, ButtonLinkProps>(
  ({ className, variant, size, children, ...props }, ref) => {
    return (
      <a
        ref={ref}
        className={cn(buttonVariants({ variant, size }), className)}
        {...props}
      >
        {children}
      </a>
    );
  },
);
ButtonLink.displayName = "ButtonLink";

// ── IconButton — bouton carré icône seule (approuver/refuser/annuler/son/
// pagination) — remplace les ActionBtn/boutons carrés dupliqués partout ──

export const iconButtonVariants = cva(
  "flex flex-shrink-0 cursor-pointer items-center justify-center rounded-full transition-colors disabled:pointer-events-none disabled:opacity-40",
  {
    variants: {
      variant: {
        neutral:
          "border border-border text-text-secondary hover:bg-surface-alt hover:text-text-primary",
        success:
          "border border-green-500/30 text-green-400 hover:bg-green-500/15",
        danger: "border border-red-500/30 text-red-400 hover:bg-red-500/15",
        // ✅ Sur backdrop/vidéo (bouton mute du carrousel/modal bande-annonce)
        dark: "bg-black/50 text-white/80 backdrop-blur-sm hover:bg-black/70 hover:text-white",
      },
      size: {
        sm: "h-8 w-8 text-base",
        md: "h-9 w-9 text-lg",
      },
    },
    defaultVariants: { variant: "neutral", size: "sm" },
  },
);

export interface IconButtonProps
  extends
    ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof iconButtonVariants> {}

export const IconButton = forwardRef<HTMLButtonElement, IconButtonProps>(
  ({ className, variant, size, children, ...props }, ref) => {
    return (
      <button
        ref={ref}
        className={cn(iconButtonVariants({ variant, size }), className)}
        {...props}
      >
        {children}
      </button>
    );
  },
);
IconButton.displayName = "IconButton";
