"use client";

import { ChevronLeft, ChevronRight } from "lucide-react";
import { IconButton } from "@/components/ui/Button";

// ✅ Façon Overseerr — deux petits boutons ronds dans l'en-tête, à côté du
// titre, plutôt que des flèches flottantes par-dessus les affiches.
// Toujours visibles, juste désactivés en bout de liste (pas de layout shift
// contrairement à un masquage conditionnel).
export function ScrollArrows({
  canScrollLeft,
  canScrollRight,
  onScrollLeft,
  onScrollRight,
}: {
  canScrollLeft: boolean;
  canScrollRight: boolean;
  onScrollLeft: () => void;
  onScrollRight: () => void;
}) {
  return (
    <div className="flex flex-shrink-0 items-center gap-1.5">
      <IconButton
        variant="neutral"
        size="sm"
        onClick={onScrollLeft}
        disabled={!canScrollLeft}
        aria-label="Défiler vers la gauche"
      >
        <ChevronLeft className="size-4" aria-hidden />
      </IconButton>
      <IconButton
        variant="neutral"
        size="sm"
        onClick={onScrollRight}
        disabled={!canScrollRight}
        aria-label="Défiler vers la droite"
      >
        <ChevronRight className="size-4" aria-hidden />
      </IconButton>
    </div>
  );
}
