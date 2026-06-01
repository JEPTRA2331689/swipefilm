namespace swipefilm.Auth
{
    // swipefilm/Services/TmdbService.cs
    using System.Text.Json;
    using global::swipefilm.Models;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;


    namespace swipefilm.Services
    {
        public class TmdbService
        {
            private readonly HttpClient _http;
            private readonly AppDbContext _db;
            private readonly string _apiKey;
            private readonly string _imageBaseUrl;

            public TmdbService(
                IHttpClientFactory httpClientFactory,
                AppDbContext db,
                IConfiguration config)
            {
                _http = httpClientFactory.CreateClient("Tmdb");
                _db = db;
                _apiKey = config["Tmdb:ApiKey"]
                    ?? throw new InvalidOperationException("Tmdb:ApiKey manquant");
                _imageBaseUrl = config["Tmdb:ImageBaseUrl"]
                    ?? "https://image.tmdb.org/t/p/w500";
            }

            // ─── Enrichir tous les films sans métadonnées ─────────────────

            public async Task EnrichAllMoviesAsync()
            {
                // Récupère tous les films dont le cache est vieux ou vide
                var movies = await _db.Movies
                    .Where(m => m.CachedAt == DateTime.MinValue
                             || m.CachedAt < DateTime.UtcNow.AddDays(-7))
                    .ToListAsync();

                foreach (var movie in movies)
                {
                    try
                    {
                        await EnrichMovieAsync(movie);
                        // Petit délai pour respecter le rate limit TMDB (50 req/sec)
                        await Task.Delay(25);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erreur TMDB pour {movie.Title}: {ex.Message}");
                    }
                }
            }

            // ─── Enrichir un film spécifique ──────────────────────────────

            public async Task EnrichMovieAsync(Movie movie)
            {
                var endpoint = movie.ContentType == "movie"
                    ? $"movie/{movie.TmdbId}"
                    : $"tv/{movie.TmdbId}";

                var response = await _http.GetAsync(
                    $"https://api.themoviedb.org/3/{endpoint}" +
                    $"?api_key={_apiKey}" +
                    $"&language=fr-FR" +
                    $"&append_to_response=credits,keywords"
                );

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine(
                        $"TMDB {response.StatusCode} pour TmdbId {movie.TmdbId}");
                    return;
                }

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();

                // Titre
                movie.Title = movie.ContentType == "movie"
                    ? json.GetProperty("title").GetString() ?? movie.Title
                    : json.GetProperty("name").GetString() ?? movie.Title;

                movie.OriginalTitle = movie.ContentType == "movie"
                    ? json.TryGetProperty("original_title", out var ot)
                        ? ot.GetString() : null
                    : json.TryGetProperty("original_name", out var on)
                        ? on.GetString() : null;

                // Synopsis
                movie.Overview = json.TryGetProperty("overview", out var ov)
                    ? ov.GetString() : null;

                // Images
                movie.PosterPath = json.TryGetProperty("poster_path", out var pp)
                    ? $"{_imageBaseUrl}{pp.GetString()}" : null;

                movie.BackdropPath = json.TryGetProperty("backdrop_path", out var bp)
                    ? $"{_imageBaseUrl}{bp.GetString()}" : null;

                // Date de sortie
                var dateStr = movie.ContentType == "movie"
                    ? json.TryGetProperty("release_date", out var rd)
                        ? rd.GetString() : null
                    : json.TryGetProperty("first_air_date", out var fd)
                        ? fd.GetString() : null;

                if (DateOnly.TryParse(dateStr, out var date))
                    movie.ReleaseDate = date;

                // Durée
                if (movie.ContentType == "movie"
                    && json.TryGetProperty("runtime", out var rt))
                    movie.RuntimeMinutes = rt.GetInt32();

                // Note et popularité
                if (json.TryGetProperty("vote_average", out var va))
                    movie.TmdbRating = va.GetSingle();

                if (json.TryGetProperty("popularity", out var pop))
                    movie.TmdbPopularity = pop.GetSingle();

                // Genres
                if (json.TryGetProperty("genres", out var genres))
                    movie.Genres = genres.EnumerateArray()
                        .Select(g => g.GetProperty("name").GetString()!)
                        .Where(g => g is not null)
                        .ToArray();

                // Keywords
                if (json.TryGetProperty("keywords", out var kw))
                {
                    var keywordList = movie.ContentType == "movie"
                        ? kw.TryGetProperty("keywords", out var kwList)
                            ? kwList : kw
                        : kw.TryGetProperty("results", out var kwRes)
                            ? kwRes : kw;

                    movie.Keywords = keywordList.EnumerateArray()
                        .Select(k => k.GetProperty("name").GetString()!)
                        .Where(k => k is not null)
                        .Take(10)
                        .ToArray();
                }

                // Credits (réalisateurs + casting)
                if (json.TryGetProperty("credits", out var credits))
                {
                    // Réalisateurs
                    if (credits.TryGetProperty("crew", out var crew))
                        movie.Directors = crew.EnumerateArray()
                            .Where(c => c.GetProperty("job").GetString() == "Director")
                            .Select(c => c.GetProperty("name").GetString()!)
                            .Where(c => c is not null)
                            .ToArray();

                    // Top 5 acteurs
                    if (credits.TryGetProperty("cast", out var cast))
                        movie.CastTop5 = cast.EnumerateArray()
                            .Take(5)
                            .Select(c => c.GetProperty("name").GetString()!)
                            .Where(c => c is not null)
                            .ToArray();
                }

                movie.CachedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            // ─── Recherche par titre (fallback si pas de TmdbId) ─────────

            public async Task<int?> SearchTmdbIdAsync(string title, int? year, string type)
            {
                var endpoint = type == "movie" ? "search/movie" : "search/tv";
                var yearParam = year.HasValue
                    ? $"&{'p'}rimary_release_year={year}" : "";

                var response = await _http.GetAsync(
                    $"https://api.themoviedb.org/3/{endpoint}" +
                    $"?api_key={_apiKey}" +
                    $"&query={Uri.EscapeDataString(title)}" +
                    $"&language=fr-FR" +
                    yearParam
                );

                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                var results = json.GetProperty("results");

                if (!results.EnumerateArray().Any()) return null;

                return results.EnumerateArray()
                    .First()
                    .GetProperty("id")
                    .GetInt32();
            }
        }
    }
}
