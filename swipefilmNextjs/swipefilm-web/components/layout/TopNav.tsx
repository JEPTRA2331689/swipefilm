"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Settings, User } from "lucide-react";
import { cn } from "@/lib/utils";

const navItems = [
  { href: "/home", label: "Accueil" },
  { href: "/swipe", label: "Swipe" },
  { href: "/search", label: "Rechercher" },
  { href: "/list", label: "Ma liste" },
];

export function TopNav() {
  const pathname = usePathname();

  return (
    <header className="border-border bg-bg-primary/85 sticky top-0 z-50 border-b backdrop-blur-md">
      <div className="flex items-center justify-between gap-6 px-4 py-4 md:px-12">
        <Link href="/home" className="flex items-baseline gap-2">
          <span className="font-display text-text-primary text-xl font-bold tracking-tight">
            Swipe<span className="text-accent">Film3</span>
          </span>
        </Link>

        <nav className="hidden items-center gap-1 md:flex">
          {navItems.map((item) => {
            const active = pathname === item.href;
            return (
              <Link
                key={item.href}
                href={item.href}
                className={cn(
                  "rounded-button px-4 py-2 text-sm font-medium transition-colors",
                  active
                    ? "text-accent bg-accent/10"
                    : "text-text-secondary hover:text-text-primary",
                )}
              >
                {item.label}
              </Link>
            );
          })}
        </nav>

        <div className="flex items-center gap-2">
          <Link
            href="/settings"
            className="bg-surface-alt border-border text-text-secondary hover:text-text-primary flex h-9 w-9 items-center justify-center rounded-full border text-sm font-medium transition-colors"
            title="Paramètres"
          >
            <Settings className="size-4" aria-hidden="true" />
          </Link>
          <Link
            href="/profile"
            className="bg-surface-alt border-border text-text-secondary hover:text-text-primary flex h-9 w-9 items-center justify-center rounded-full border text-sm font-medium transition-colors"
          >
            <User className="size-4" aria-hidden="true" />
          </Link>
        </div>
      </div>
    </header>
  );
}
