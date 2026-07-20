import { NextResponse } from "next/server";

const TMDB_API_KEY = process.env.TMDB_API_KEY;
const TMDB_BASE = "https://api.themoviedb.org/3";
const IMG_BASE = "https://image.tmdb.org/t/p";

export async function GET(
  _req: Request,
  { params }: { params: Promise<{ id: string }> },
) {
  const { id } = await params;

  if (!TMDB_API_KEY) {
    return NextResponse.json(
      { error: "TMDB_API_KEY manquante" },
      { status: 500 },
    );
  }

  try {
    const res = await fetch(
      `${TMDB_BASE}/movie/${id}?api_key=${TMDB_API_KEY}&language=fr-FR&append_to_response=credits`,
      { next: { revalidate: 86400 } }, // cache 24h
    );

    if (!res.ok) {
      return NextResponse.json(
        { error: "Film introuvable" },
        { status: res.status },
      );
    }

    const data = await res.json();

    const directors = (data.credits?.crew ?? [])
      .filter((c: { job: string }) => c.job === "Director")
      .map((c: { name: string }) => c.name)
      .slice(0, 3);

    const cast = (data.credits?.cast ?? [])
      .slice(0, 5)
      .map(
        (c: {
          name: string;
          character: string;
          profile_path: string | null;
        }) => ({
          name: c.name,
          character: c.character,
          profilePath: c.profile_path
            ? `${IMG_BASE}/w185${c.profile_path}`
            : null,
        }),
      );

    const backdropPath = data.backdrop_path
      ? `${IMG_BASE}/original${data.backdrop_path}`
      : null;

    return NextResponse.json({
      directors,
      cast,
      backdropPath,
      tagline: data.tagline ?? null,
    });
  } catch {
    return NextResponse.json({ error: "Erreur réseau" }, { status: 502 });
  }
}
