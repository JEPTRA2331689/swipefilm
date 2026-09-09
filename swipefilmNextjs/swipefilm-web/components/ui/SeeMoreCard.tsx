"use client";

import Link from "next/link";
import { ChevronRight } from "lucide-react";
import { cn } from "@/lib/utils";

// ✅ Façon Overseerr — la carte "Voir plus" vit à la fin de la liste elle-même
// (même gabarit que les affiches) plutôt qu'un lien texte dans l'en-tête.
export function SeeMoreCard({
  href,
  className,
}: {
  href: string;
  className?: string;
}) {
  return (
    <Link
      href={href}
      className={cn(
        "group bg-surface-alt hover:bg-surface-alt/70 flex flex-shrink-0 flex-col items-center justify-center gap-2 rounded-lg transition-colors",
        className,
      )}
    >
      <div className="border-border group-hover:border-accent group-hover:text-accent text-text-secondary flex size-11 items-center justify-center rounded-full border-2 transition-colors">
        <ChevronRight className="size-5" aria-hidden />
      </div>
      <span className="text-text-secondary group-hover:text-accent text-xs font-medium transition-colors">
        Voir plus
      </span>
    </Link>
  );
}
