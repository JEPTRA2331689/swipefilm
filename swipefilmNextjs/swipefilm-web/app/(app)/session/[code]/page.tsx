import SessionClient from "./SessionClient";

// ✅ Export statique — /session/* partage un seul shell ("_"), le vrai code
// est lu dans l'URL du navigateur au chargement (useStaticRouteId), pas au
// build — même pattern que /movie/[id] et /series/[id].
export function generateStaticParams() {
  return [{ code: "_" }];
}

export default function SessionPage() {
  return <SessionClient />;
}
