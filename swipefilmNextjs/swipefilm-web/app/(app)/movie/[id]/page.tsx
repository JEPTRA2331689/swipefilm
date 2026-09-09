import MovieDetailClient from "./MovieDetailClient";

// ✅ Export statique servi par le backend .NET (pas de serveur Node en
// prod) — la page ne peut pas être pré-rendue pour chaque id de film
// possible. Le backend sert ce même shell pour toute URL /movie/*, et
// MovieDetailClient lit le vrai id dans l'URL du navigateur au chargement
// (useParams), au lieu de dépendre d'un param connu au build.
// generateStaticParams doit vivre dans un Server Component — d'où ce
// wrapper séparé de la logique cliente.
export function generateStaticParams() {
  return [{ id: "_" }];
}

export default function MovieDetailPage() {
  return <MovieDetailClient />;
}
