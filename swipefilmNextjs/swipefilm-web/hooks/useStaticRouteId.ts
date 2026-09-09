"use client";

import { useEffect, useState } from "react";

// ✅ Export statique — /movie/* et /series/* partagent tous un seul shell
// HTML ("_", généré au build par generateStaticParams — voir page.tsx dans
// ces dossiers). Sur un chargement direct ou un rafraîchissement de
// /movie/123, useParams() renvoie ce "_" figé (il vient du payload
// embarqué dans le shell servi par le backend, pas de l'URL réelle) — d'où
// ce hook, qui lit le dernier segment de window.location.pathname à la
// place : ça reflète toujours la vraie URL du navigateur, peu importe
// comment la page a été chargée.
export function useStaticRouteId(): string | null {
  const [id, setId] = useState<string | null>(null);

  useEffect(() => {
    const segments = window.location.pathname.split("/").filter(Boolean);
    setId(segments[segments.length - 1] ?? null);
  }, []);

  return id;
}
