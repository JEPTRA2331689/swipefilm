import { Bookmark, Home, Layers, Search, type LucideIcon } from "lucide-react";

export const navItems: { href: string; label: string; icon: LucideIcon }[] = [
  { href: "/home", label: "Accueil", icon: Home },
  { href: "/swipe", label: "Swipe", icon: Layers },
  { href: "/search", label: "Rechercher", icon: Search },
  { href: "/list", label: "Ma liste", icon: Bookmark },
];

export function isNavActive(pathname: string, href: string) {
  return pathname === href || pathname.startsWith(`${href}/`);
}
