"use client";

import { cn } from "@/lib/utils";

export function PermToggle({
  label,
  description,
  checked,
  onChange,
}: {
  label: string;
  description: string;
  checked: boolean;
  onChange: () => void;
}) {
  return (
    <div
      onClick={onChange}
      className="border-border group flex cursor-pointer items-center justify-between gap-4 border-b py-3 last:border-0"
    >
      <div>
        <p className="text-text-primary group-hover:text-accent text-sm font-medium transition-colors">
          {label}
        </p>
        <p className="text-text-secondary mt-0.5 text-xs">{description}</p>
      </div>
      <div
        className={cn(
          "relative h-6 w-10 flex-shrink-0 rounded-full transition-colors",
          checked ? "bg-accent" : "bg-surface-alt border-border border",
        )}
      >
        <span
          className={cn(
            "absolute top-1 h-4 w-4 rounded-full bg-white shadow transition-transform",
            checked ? "translate-x-5" : "translate-x-1",
          )}
        />
      </div>
    </div>
  );
}
