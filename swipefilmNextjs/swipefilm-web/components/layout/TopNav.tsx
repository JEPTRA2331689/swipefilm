"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { Settings, User } from "lucide-react";
import { cn } from "@/lib/utils";
import { isNavActive, navItems } from "./navItems";

export function TopNav() {
  const pathname = usePathname();
  const [hidden, setHidden] = useState(false);
  const hiddenRef = useRef(false);

  useEffect(() => {
    let lastY = window.scrollY;
    function onScroll() {
      const y = window.scrollY;
      const next = y > lastY && y > 80;
      lastY = y;
      if (next !== hiddenRef.current) {
        hiddenRef.current = next;
        setHidden(next);
      }
    }
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  return (
    <header
      className={cn(
        "border-border bg-bg-primary/85 sticky top-0 z-50 border-b backdrop-blur-md transition-transform duration-300",
        hidden && "-translate-y-full",
      )}
    >
      <div className="flex items-center justify-between gap-6 px-4 py-4 md:px-12">
        <Link href="/home" className="flex items-baseline gap-2">
          <span className="font-display text-text-primary text-xl font-bold tracking-tight">
            Swipe<span className="text-accent">Film</span>
          </span>
        </Link>

        <nav className="hidden items-center gap-1 md:flex">
          {navItems.map((item) => {
            const active = isNavActive(pathname, item.href);
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
