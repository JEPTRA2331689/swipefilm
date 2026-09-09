import SeriesDetailClient from "./SeriesDetailClient";

// ✅ Même raison que movie/[id] — export statique, pas de pré-rendu par id
// possible, le backend sert ce shell pour toute URL /series/*.
export function generateStaticParams() {
  return [{ id: "_" }];
}

export default function SeriesDetailPage() {
  return <SeriesDetailClient />;
}
