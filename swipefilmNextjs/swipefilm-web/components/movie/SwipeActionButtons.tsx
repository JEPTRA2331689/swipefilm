import { Heart, ThumbsUp, X, ThumbsDown } from "lucide-react";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/Button";

interface SwipeActionButtonsProps {
  swipeState: "idle" | "liked" | "disliked";
  onSwipe: (direction: "Right" | "Left") => void;
  disabled?: boolean;
  className?: string;
}

const iconCls = "size-4 text-black transition-transform duration-150 group-hover/btn:scale-125";
const tiltCls = "group-hover/btn:-rotate-12";

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
        className={cn("group/btn gap-2 px-4 py-2 font-bold", className)}
      >
        {swipeState === "disliked" ? (
          <X className={iconCls} fill="currentColor" />
        ) : (
          <ThumbsDown className={cn(iconCls, tiltCls)} fill="currentColor" />
        )}
        {swipeState === "disliked" ? "Noté" : "dislike"}
      </Button>

      <Button
        variant="like"
        onClick={() => onSwipe("Right")}
        disabled={disabled}
        className={cn("group/btn gap-2 px-4 py-2 font-bold", className)}
      >
        {swipeState === "liked" ? (
          <Heart className={iconCls} fill="currentColor" />
        ) : (
          <ThumbsUp className={cn(iconCls, tiltCls)} fill="currentColor" />
        )}
        {swipeState === "liked" ? "Aimé !" : "like"}
      </Button>
    </>
  );
}
