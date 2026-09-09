import { Check } from "lucide-react";
import { cn } from "@/lib/utils";

interface SetupStepsProps {
  stepNumber: number;
  description: string;
  active?: boolean;
  completed?: boolean;
  isLastStep?: boolean;
}

// ✅ Repris du pattern Overseerr (src/components/Setup/SetupSteps.tsx, MIT) —
// cercles numérotés reliés par un chevron, plutôt qu'une barre de
// progression. Complété = rempli + coche, actif = bord accent, à venir =
// neutre.
export function SetupSteps({
  stepNumber,
  description,
  active = false,
  completed = false,
  isLastStep = false,
}: SetupStepsProps) {
  return (
    <li className="relative md:flex md:flex-1">
      <div className="flex items-center gap-4 px-6 py-4 text-sm font-medium">
        <div
          className={cn(
            "flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-full border-2",
            active && "border-accent",
            !active && !completed && "border-border-strong",
            completed && "border-accent bg-accent",
          )}
        >
          {completed ? (
            <Check className="text-bg-primary size-5" />
          ) : (
            <p className={active ? "text-text-primary" : "text-text-secondary"}>
              {stepNumber}
            </p>
          )}
        </div>
        <p
          className={cn(
            "text-sm font-medium",
            active ? "text-text-primary" : "text-text-secondary",
          )}
        >
          {description}
        </p>
      </div>

      {!isLastStep && (
        <div className="absolute top-0 right-0 hidden h-full w-5 md:block">
          <svg
            className="text-border-strong h-full w-full"
            viewBox="0 0 22 80"
            fill="none"
            preserveAspectRatio="none"
          >
            <path
              d="M0 -2L20 40L0 82"
              vectorEffect="non-scaling-stroke"
              stroke="currentColor"
              strokeLinejoin="round"
            />
          </svg>
        </div>
      )}
    </li>
  );
}
