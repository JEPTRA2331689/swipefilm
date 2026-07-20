import { NextRequest, NextResponse } from "next/server";
import { Permission, hasPermission } from "@/lib/permissions";
import { decodeJwt, isTokenExpired } from "@/lib/jwt";

const COOKIE = "swipefilm_token";

const PUBLIC_ROUTES = ["/", "/login", "/onboarding"];

// Ordre important : les routes plus spécifiques en premier
const ROUTE_PERMISSIONS: Array<{ pattern: RegExp; permission: Permission }> = [
  { pattern: /^\/admin\/users(\/|$)/,       permission: Permission.ManageUsers },
  { pattern: /^\/admin\/servers(\/|$)/,     permission: Permission.Admin },
  { pattern: /^\/admin\/permissions(\/|$)/, permission: Permission.ManageUsers },
  { pattern: /^\/admin(\/|$)/,              permission: Permission.ViewAdminDashboard },
  { pattern: /^\/settings(\/|$)/,           permission: Permission.CanSwipe },
  { pattern: /^\/movie(\/|$)/,              permission: Permission.CanSwipe },
  { pattern: /^\/history(\/|$)/,            permission: Permission.CanSwipe },
  { pattern: /^\/profile(\/|$)/,            permission: Permission.CanSwipe },
  { pattern: /^\/search(\/|$)/,             permission: Permission.CanSwipe },
  { pattern: /^\/watchlist(\/|$)/,          permission: Permission.CanSwipe },
  { pattern: /^\/swipe(\/|$)/,              permission: Permission.CanSwipe },
  { pattern: /^\/home(\/|$)/,               permission: Permission.CanSwipe },
  { pattern: /^\/list(\/|$)/,               permission: Permission.CanSwipe },
];

export function middleware(req: NextRequest) {
  const { pathname } = req.nextUrl;

  // Laisse passer les ressources statiques et les routes Next.js internes
  if (
    pathname.startsWith("/_next") ||
    pathname.startsWith("/api/") ||
    pathname.startsWith("/favicon") ||
    pathname.includes(".")
  ) {
    return NextResponse.next();
  }

  // Routes publiques → toujours OK
  if (PUBLIC_ROUTES.some((r) => pathname === r || pathname.startsWith(r + "/"))) {
    return NextResponse.next();
  }

  const token = req.cookies.get(COOKIE)?.value;

  // Pas de token → login
  if (!token) {
    return NextResponse.redirect(new URL("/login", req.url));
  }

  const payload = decodeJwt(token);

  // Token invalide ou expiré → login
  if (!payload || isTokenExpired(payload)) {
    const res = NextResponse.redirect(new URL("/login", req.url));
    res.cookies.delete(COOKIE);
    return res;
  }

  const userPerms = parseInt(payload.permissions ?? "0", 10);

  // Vérifie la permission requise pour cette route
  for (const { pattern, permission } of ROUTE_PERMISSIONS) {
    if (pattern.test(pathname)) {
      if (!hasPermission(userPerms, permission)) {
        return NextResponse.redirect(new URL("/unauthorized", req.url));
      }
      break;
    }
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon.ico).*)"],
};