import { type ClassValue, clsx } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

export function tmdbImage(
  path: string | null,
  size:
    | "w92"
    | "w154"
    | "w185"
    | "w300"
    | "w342"
    | "w500"
    | "w780"
    | "w1280"
    | "original" = "w500",
) {
  if (!path) return null;
  if (path.startsWith("http")) return path;
  return `https://image.tmdb.org/t/p/${size}${path}`;
}

export function formatRuntime(minutes: number | null) {
  if (!minutes) return null;
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  return h > 0 ? `${h}h${m.toString().padStart(2, "0")}` : `${m}min`;
}

export function releaseYear(date: string | null) {
  if (!date) return null;
  return date.slice(0, 4);
}

// ✅ Alterne deux listes (ex: films/séries) au lieu de les concaténer — évite
// un carrousel "tous les films puis toutes les séries".
export function interleave<T>(a: T[], b: T[]): T[] {
  const result: T[] = [];
  const max = Math.max(a.length, b.length);
  for (let i = 0; i < max; i++) {
    if (i < a.length) result.push(a[i]);
    if (i < b.length) result.push(b[i]);
  }
  return result;
}
