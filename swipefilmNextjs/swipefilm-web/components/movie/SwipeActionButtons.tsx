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
  return (
    <>
      <Button
        variant="like"
        onClick={() => onSwipe("Right")}
        disabled={disabled}
        className={cn("gap-2 px-4 py-2 font-bold", className)}
      >
        <i
          className={cn(
            "ph-fill text-base text-black",
            swipeState === "liked" ? "ph-heart" : "ph-thumbs-up",
          )}
        />
        {swipeState === "liked" ? "Aimé !" : "like"}
      </Button>

      <Button
        variant="skip"
        onClick={() => onSwipe("Left")}
        disabled={disabled}
        className={cn("gap-2 px-4 py-2 font-bold", className)}
      >
        <i
          className={cn(
            "ph-fill text-base text-black",
            swipeState === "disliked" ? "ph-x" : "ph-thumbs-down",
          )}
        />
        {swipeState === "disliked" ? "Noté" : "dislike"}
      </Button>
    </>
  );
}
