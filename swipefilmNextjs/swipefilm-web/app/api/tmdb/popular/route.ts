import { NextResponse } from "next/server";

const TMDB_API_KEY = process.env.TMDB_API_KEY;
const TMDB_BASE = "https://api.themoviedb.org/3";

export async function GET() {
  if (!TMDB_API_KEY) {
    return NextResponse.json(
      { error: "TMDB_API_KEY non configurée" },
      { status: 500 },
    );
  }

  try {
    const res = await fetch(
      `${TMDB_BASE}/movie/popular?api_key=${TMDB_API_KEY}&language=fr-FR&page=1`,
      { next: { revalidate: 3600 } },
    );

    if (!res.ok) {
      return NextResponse.json(
        { error: "Échec de la requête TMDB" },
        { status: res.status },
      );
    }

    const data = await res.json();

    const posters = (data.results ?? [])
      .filter((m: { poster_path: string | null }) => m.poster_path)
      .slice(0, 18)
      .map(
        (m: {
          id: number;
          title: string;
          poster_path: string;
          vote_average: number;
          release_date: string;
        }) => ({
          id: String(m.id),
          tmdbId: m.id,
          title: m.title,
          posterPath: m.poster_path,
          overview: null,
          tmdbRating: m.vote_average,
          runtimeMinutes: null,
          genres: [],
          releaseDate: m.release_date || null,
          contentType: "movie" as const,
          isAvailable: false,
        }),
      );

    return NextResponse.json({ posters });
  } catch {
    return NextResponse.json(
      { error: "Erreur réseau lors de la requête TMDB" },
      { status: 502 },
    );
  }
}
