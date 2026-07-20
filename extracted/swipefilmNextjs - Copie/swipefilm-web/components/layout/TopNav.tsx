"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
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
    <header className="sticky top-0 z-50 border-b border-border bg-bg-primary/85 backdrop-blur-md">
      <div className="flex items-center justify-between gap-6 px-4 py-4 md:px-12">
        <Link href="/home" className="flex items-baseline gap-2">
          <span className="font-display text-xl font-bold tracking-tight text-text-primary">
            Swipe<span className="text-accent">Film</span>
          </span>
        </Link>

        <nav className="hidden md:flex items-center gap-1">
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
                    : "text-text-secondary hover:text-text-primary"
                )}
              >
                {item.label}
              </Link>
            );
          })}
        </nav>

        <Link
          href="/profile"
          className="h-9 w-9 rounded-full bg-surface-alt border border-border flex items-center justify-center text-sm font-medium text-text-secondary hover:text-text-primary transition-colors"
        >
          <i className="ti ti-user" aria-hidden="true" />
        </Link>
      </div>
    </header>
  );
}
