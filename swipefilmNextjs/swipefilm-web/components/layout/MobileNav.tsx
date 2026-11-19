"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { cn } from "@/lib/utils";
import { isNavActive, navItems } from "./navItems";

export function MobileNav() {
  const pathname = usePathname();

  return (
    <nav
      aria-label="Navigation principale"
      className="border-border bg-bg-primary/90 fixed inset-x-0 bottom-0 z-50 border-t pb-[env(safe-area-inset-bottom)] backdrop-blur-md md:hidden"
    >
      <ul className="grid h-16 grid-cols-4">
        {navItems.map(({ href, label, icon: Icon }) => {
          const active = isNavActive(pathname, href);
          return (
            <li key={href}>
              <Link
                href={href}
                aria-current={active ? "page" : undefined}
                className={cn(
                  "flex h-full flex-col items-center justify-center gap-1 text-[11px] font-medium transition-colors",
                  active ? "text-accent" : "text-text-secondary hover:text-text-primary",
                )}
              >
                <Icon className="size-5" aria-hidden="true" strokeWidth={active ? 2.5 : 2} />
                {label}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
