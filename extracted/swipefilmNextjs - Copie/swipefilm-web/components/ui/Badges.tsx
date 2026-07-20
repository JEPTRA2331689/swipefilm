import { cn } from "@/lib/utils";

export function AvailabilityDot({ isAvailable }: { isAvailable: boolean }) {
  return (
    <div
      className={cn(
        "h-full aspect-square rounded-full ring-5 ring-bg-primary/60",
        isAvailable ? "bg-swipe-star" : "bg-text-secondary/40"
      )}
      role="img"
      aria-label={isAvailable ? "Disponible" : "Non disponible"}
      title={isAvailable ? "Disponible" : "Non disponible"}
    />
  );
}

export function ScoreBadge({ rating, size = "md" }: { rating: number; size?: "sm" | "md" | "lg" }) {
  const sizeClasses = {
    sm: "text-sm",
    md: "text-lg",
    lg: "text-2xl",
  };

  return (
    <span
      className={cn(
        "font-display font-bold text-text-primary drop-shadow-[0_1px_4px_rgba(0,0,0,0.6)]",
        sizeClasses[size]
      )}
    >
      {rating.toFixed(1)}
      <span className="text-swipe-star">*</span>
    </span>
  );
}
