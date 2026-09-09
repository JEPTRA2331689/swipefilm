import { Heart, ThumbsUp, X, ThumbsDown } from "lucide-react";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/Button";

interface SwipeActionButtonsProps {
  swipeState: "idle" | "liked" | "disliked";
  onSwipe: (direction: "Right" | "Left") => void;
  disabled?: boolean;
  className?: string;
}

// ✅ Like/Dislike partagés fiche film + fiche série + deck de swipe — fond
// plein, texte noir gras, icône directement accolée au texte.
export function SwipeActionButtons({
  swipeState,
  onSwipe,
  disabled,
  className,
}: SwipeActionButtonsProps) {
  // ✅ Ordre = sens du swipe : dislike (swipe gauche) à gauche, like (swipe
  // droite) à droite — inversé avant (like rendu en premier = à gauche).
  return (
    <>
      <Button
        variant="skip"
        onClick={() => onSwipe("Left")}
        disabled={disabled}
        className={cn("gap-2 px-4 py-2 font-bold", className)}
      >
        {swipeState === "disliked" ? (
          <X className="size-4 text-black" fill="currentColor" />
        ) : (
          <ThumbsDown className="size-4 text-black" fill="currentColor" />
        )}
        {swipeState === "disliked" ? "Noté" : "dislike"}
      </Button>

      <Button
        variant="like"
        onClick={() => onSwipe("Right")}
        disabled={disabled}
        className={cn("gap-2 px-4 py-2 font-bold", className)}
      >
        {swipeState === "liked" ? (
          <Heart className="size-4 text-black" fill="currentColor" />
        ) : (
          <ThumbsUp className="size-4 text-black" fill="currentColor" />
        )}
        {swipeState === "liked" ? "Aimé !" : "like"}
      </Button>
    </>
  );
}
