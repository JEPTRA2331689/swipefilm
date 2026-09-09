"use client";

import { Moon, Sun, Check, type LucideIcon } from "lucide-react";
import { useTheme } from "@/hooks/useTheme";
import { SectionCard, CardBody } from "@/shared/ui/FormPrimitives";
import { cn } from "@/lib/utils";

export function AppearanceSection() {
  const { theme, setTheme } = useTheme();

  const options: {
    value: "dark" | "light";
    label: string;
    icon: LucideIcon;
    description: string;
  }[] = [
    {
      value: "dark",
      label: "Sombre",
      icon: Moon,
      description: "Interface noire, idéale en soirée",
    },
    {
      value: "light",
      label: "Clair",
      icon: Sun,
      description: "Interface claire pour la journée",
    },
  ];

  return (
    <SectionCard>
      <CardBody>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          {options.map((opt) => {
            const isActive = theme === opt.value;
            return (
              <button
                key={opt.value}
                onClick={() => setTheme(opt.value)}
                className={cn(
                  "relative flex cursor-pointer flex-col items-start gap-3 rounded-xl border-2 p-4 text-left transition-all",
                  isActive
                    ? "border-accent bg-accent/10 shadow-accent/20 shadow-sm"
                    : "border-border bg-surface-alt hover:border-border hover:bg-surface",
                )}
              >
                {/* Preview thumbnail */}
                <div
                  className={cn(
                    "flex h-20 w-full flex-col overflow-hidden rounded-lg border",
                    opt.value === "dark"
                      ? "border-white/10 bg-[#0A130D]"
                      : "border-black/10 bg-[#F5F0E8]",
                  )}
                >
                  {/* Fake topbar */}
                  <div
                    className={cn(
                      "flex h-5 items-center gap-1.5 px-3",
                      opt.value === "dark" ? "bg-[#111E14]" : "bg-[#EDE8DC]",
                    )}
                  >
                    <span className="bg-accent/70 h-2 w-2 rounded-full" />
                    <span
                      className={cn(
                        "h-1.5 w-16 rounded-full",
                        opt.value === "dark" ? "bg-white/10" : "bg-black/10",
                      )}
                    />
                  </div>
                  {/* Fake content */}
                  <div className="flex flex-1 flex-col gap-1.5 px-3 pt-2">
                    <span
                      className={cn(
                        "h-2 w-3/4 rounded-full",
                        opt.value === "dark" ? "bg-white/15" : "bg-black/15",
                      )}
                    />
                    <span
                      className={cn(
                        "h-1.5 w-1/2 rounded-full",
                        opt.value === "dark" ? "bg-white/8" : "bg-black/8",
                      )}
                    />
                  </div>
                </div>

                <div className="flex w-full items-start justify-between gap-2">
                  <div>
                    <div className="flex items-center gap-2">
                      <opt.icon
                        className={cn(
                          "size-5",
                          isActive ? "text-accent" : "text-text-secondary",
                        )}
                      />
                      <span
                        className={cn(
                          "text-sm font-semibold",
                          isActive ? "text-accent" : "text-text-primary",
                        )}
                      >
                        {opt.label}
                      </span>
                    </div>
                    <p className="text-text-secondary mt-0.5 text-xs">
                      {opt.description}
                    </p>
                  </div>
                  {/* Checkmark */}
                  <span
                    className={cn(
                      "mt-0.5 flex h-5 w-5 flex-shrink-0 items-center justify-center rounded-full border-2 transition-all",
                      isActive ? "border-accent bg-accent" : "border-border",
                    )}
                  >
                    {isActive && (
                      <Check className="size-2.5 text-white" />
                    )}
                  </span>
                </div>
              </button>
            );
          })}
        </div>
      </CardBody>
    </SectionCard>
  );
}
