// swipefilm/Services/PersonalizedDiscoveryService.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using swipefilm.Auth.swipefilm.Services;
using swipefilm.Data;
using swipefilm.Models;

namespace swipefilm.Auth
{
    public class PersonalizedDiscoveryService
    {
        private readonly HttpClient _http;
        private readonly AppDbContext _db;
        private readonly TmdbService _tmdb;
        private readonly string _apiKey;
        private readonly IServiceProvider _serviceProvider; // ← ajouter

        public PersonalizedDiscoveryService(
            IHttpClientFactory factory,
            AppDbContext db,
            TmdbService tmdb,
            IConfiguration config,
            IServiceProvider serviceProvider)
        {
            _http = factory.CreateClient("Tmdb");
            _db = db;
            _tmdb = tmdb;
            _apiKey = config["Tmdb:ApiKey"]!;
            _serviceProvider = serviceProvider;
        }

        public async Task DiscoverForUserAsync(Guid userId)
        {
            var profile = await _db.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            // Films que l'user a vraiment aimés (signal fort)
            var lovedMovies = await GetLovedMoviesAsync(userId);
            Console.WriteLine($"[Discovery] {lovedMovies.Count} films aimés trouvés");

            if (!lovedMovies.Any() && profile is null)
            {
                // Cold start → on ne fait rien encore
                // L'algo de reco gère ça avec la popularité
                return;
            }

            var discoveredIds = new HashSet<int>();

            // ── Stratégie 1 : Recommandations TMDB basées sur films aimés
            foreach (var movie in lovedMovies.Take(10))
            {
                var similar = await FetchSimilarAsync(movie.TmdbId, movie.ContentType);
                foreach (var id in similar) discoveredIds.Add(id);

                var recs = await FetchTmdbRecommendationsAsync(movie.TmdbId, movie.ContentType);
                foreach (var id in recs) discoveredIds.Add(id);
            }

            // ── Stratégie 2 : Discover par réalisateurs favoris
            if (profile?.DirectorWeights.Any() == true)
            {
                var topDirectors = profile.DirectorWeights
                    .Where(d => d.Value > 0.5f)
                    .OrderByDescending(d => d.Value)
                    .Take(5)
                    .Select(d => d.Key)
                    .ToList();

                foreach (var director in topDirectors)
                {
                    var films = await FetchByPersonAsync(director);
                    foreach (var id in films) discoveredIds.Add(id);
                }
            }

            // ── Stratégie 3 : Discover par acteurs favoris
            if (profile?.ActorWeights.Any() == true)
            {
                var topActors = profile.ActorWeights
                    .Where(a => a.Value > 0.6f)
                    .OrderByDescending(a => a.Value)
                    .Take(5)
                    .Select(a => a.Key)
                    .ToList();

                foreach (var actor in topActors)
                {
                    var films = await FetchByPersonAsync(actor);
                    foreach (var id in films) discoveredIds.Add(id);
                }

            }

            // ── Stratégie 4 : Discover par combinaison de genres
            // Pas juste un genre, mais la combinaison précise
            if (profile?.GenreWeights.Any() == true)
            {
                var topGenres = profile.GenreWeights
                    .Where(g => g.Value > 0.5f)
                    .OrderByDescending(g => g.Value)
                    .Take(4)
                    .Select(g => g.Key)
                    .ToList();

                // Combinaisons de 2 genres (plus précis qu'un seul)
                for (int i = 0; i < topGenres.Count - 1; i++)
                {
                    var combo = new[] { topGenres[i], topGenres[i + 1] };
                    var films = await FetchByGenreComboAsync(
                        combo, profile.PreferredRuntimeMax);
                    foreach (var id in films) discoveredIds.Add(id);
                }
            }

            // ── Stratégie 5 : Keywords profonds
            // Si tu aimes "psychological thriller" + "unreliable narrator"
            // → trouve des films avec ces deux keywords précis
            if (profile?.KeywordWeights.Any() == true)
            {
                var topKeywords = profile.KeywordWeights
                    .Where(k => k.Value > 0.6f)
                    .OrderByDescending(k => k.Value)
                    .Take(3)
                    .Select(k => k.Key)
                    .ToList();

                var films = await FetchByKeywordsAsync(topKeywords);
                foreach (var id in films) discoveredIds.Add(id);
            }

            // ── Stratégie 6 : Époque préférée
            // Si tu regardes surtout des films des années 90-2000
            // → creuse dans cette époque
            if (lovedMovies.Any())
            {
                var avgYear = lovedMovies
                    .Where(m => m.ReleaseDate.HasValue)
                    .Average(m => (double)m.ReleaseDate!.Value.Year);

                if (avgYear > 0)
                {
                    var films = await FetchByEraAsync(
                        (int)(avgYear - 10),
                        (int)(avgYear + 5),
                        profile?.GenreWeights
                            .OrderByDescending(g => g.Value)
                            .FirstOrDefault().Key);
                    foreach (var id in films) discoveredIds.Add(id);
                }
            }

            // Filtrer films déjà en BD
            var existingIds = await _db.Movies
                .Select(m => m.TmdbId)
                .ToListAsync();

            var newIds = discoveredIds
                .Where(id => !existingIds.Contains(id))
                .ToList();

            // Ajouter en BD
            foreach (var id in newIds)
            {
                _db.Movies.Add(new Movie
                {
                    Id = Guid.NewGuid(),
                    TmdbId = id,
                    Title = "",
                    ContentType = "movie",
                    CachedAt = DateTime.MinValue
                });
            }

            if (newIds.Any())
                await _db.SaveChangesAsync();

            // Enrichir immédiatement les nouveaux films
            var newMovies = await _db.Movies
                .Where(m => newIds.Contains(m.TmdbId))
                .ToListAsync();

            foreach (var movie in newMovies)
            {
                await _tmdb.EnrichMovieAsync(movie);
                await Task.Delay(25); // Rate limit TMDB
            }
        }

        // ─── Films vraiment aimés ─────────────────────────────────────

        private async Task<List<Movie>> GetLovedMoviesAsync(Guid userId)
        {
            var history = await _db.WatchHistory
                .Include(w => w.Movie)
                .Where(w => w.UserId == userId && w.Movie != null)
                .ToListAsync();

            var scored = history
                .Select(w =>
                {
                    var completion = w.Movie!.RuntimeMinutes > 0
                        ? (float)w.WatchDurationSec
                            / (w.Movie.RuntimeMinutes!.Value * 60) * 100f
                        : 0f;

                    float score = completion / 100f;
                    if (w.UserRating.HasValue) score += w.UserRating.Value / 10f;
                    if (w.IsFavorite) score += 0.5f;
                    if (w.ViewCount > 1) score += 0.3f * (w.ViewCount - 1);

                    return (Movie: w.Movie!, Score: score);
                })
                .OrderByDescending(x => x.Score)
                .ToList();

            // ✅ Seuil progressif — prend les meilleurs même si score bas
            var loved = scored
                .Where(x => x.Score > 0.8f)
                .Select(x => x.Movie)
                .Take(20)
                .ToList();

            // Fallback 1 : si moins de 3 films aimés → prend top 10 peu importe le score
            if (loved.Count < 3)
            {
                loved = scored
                    .Select(x => x.Movie)
                    .Take(10)
                    .ToList();
            }

            // Fallback 2 : pas d'historique du tout → prend films au hasard en BD
            if (!loved.Any())
            {
                loved = await _db.Movies
                    .Where(m => m.CachedAt != DateTime.MinValue
                             && m.TmdbRating > 6.0f)
                    .OrderByDescending(m => m.TmdbRating)
                    .Take(10)
                    .ToListAsync();
            }

            return loved;
        }

        // ─── TMDB Similar ─────────────────────────────────────────────

        private async Task<List<int>> FetchSimilarAsync(
            int tmdbId, string type)
        {
            var endpoint = type == "movie"
                ? $"movie/{tmdbId}/similar"
                : $"tv/{tmdbId}/similar";

            return await FetchIdsFromEndpointAsync(endpoint);
        }

        // ─── TMDB Recommendations ─────────────────────────────────────

        private async Task<List<int>> FetchTmdbRecommendationsAsync(
            int tmdbId, string type)
        {
            var endpoint = type == "movie"
                ? $"movie/{tmdbId}/recommendations"
                : $"tv/{tmdbId}/recommendations";

            return await FetchIdsFromEndpointAsync(endpoint);
        }

        // ─── Par personne (réalisateur ou acteur) ─────────────────────

        private async Task<List<int>> FetchByPersonAsync(string name)
        {
            // Cherche l'ID de la personne
            var searchResp = await _http.GetAsync(
                $"https://api.themoviedb.org/3/search/person" +
                $"?api_key={_apiKey}&query={Uri.EscapeDataString(name)}");

            if (!searchResp.IsSuccessStatusCode) return [];

            var searchJson = await searchResp.Content
                .ReadFromJsonAsync<JsonElement>();

            var results = searchJson.GetProperty("results");
            if (!results.EnumerateArray().Any()) return [];

            var personId = results.EnumerateArray()
                .First()
                .GetProperty("id")
                .GetInt32();

            // Récupère sa filmographie
            var creditsResp = await _http.GetAsync(
                $"https://api.themoviedb.org/3/person/{personId}/movie_credits" +
                $"?api_key={_apiKey}");

            if (!creditsResp.IsSuccessStatusCode) return [];

            var creditsJson = await creditsResp.Content
                .ReadFromJsonAsync<JsonElement>();

            // Prend ses films les mieux notés (pas les plus populaires)
            return creditsJson.GetProperty("crew")
                .EnumerateArray()
                .Where(c => c.GetProperty("job").GetString() == "Director")
                .Concat(creditsJson.GetProperty("cast").EnumerateArray())
                .Where(f => f.TryGetProperty("vote_average", out var v)
                         && v.GetDouble() > 6.5
                         && f.TryGetProperty("vote_count", out var vc)
                         && vc.GetInt32() > 100)
                .OrderByDescending(f =>
                    f.GetProperty("vote_average").GetDouble())
                .Take(10)
                .Select(f => f.GetProperty("id").GetInt32())
                .ToList();
        }

        // ─── Par combinaison de genres ────────────────────────────────

        private async Task<List<int>> FetchByGenreComboAsync(
            string[] genreNames, float preferredRuntime)
        {
            var genreMap = await GetGenreMapAsync();

            var genreIds = genreNames
                .Where(g => genreMap.ContainsKey(g))
                .Select(g => genreMap[g])
                .ToList();

            if (!genreIds.Any()) return [];

            var genreParam = string.Join(",", genreIds);

            // Filtre aussi par durée préférée
            int maxRuntime = (int)Math.Min(preferredRuntime + 30, 240);

            return await FetchIdsFromEndpointAsync(
                $"discover/movie" +
                $"?with_genres={genreParam}" +
                $"&with_runtime.lte={maxRuntime}" +
                $"&vote_average.gte=6.5" +
                $"&vote_count.gte=200" +
                $"&sort_by=vote_average.desc"
            );
        }

        // ─── Par keywords ─────────────────────────────────────────────

        private async Task<List<int>> FetchByKeywordsAsync(List<string> keywords)
        {
            var keywordIds = new List<int>();

            foreach (var keyword in keywords)
            {
                var resp = await _http.GetAsync(
                    $"https://api.themoviedb.org/3/search/keyword" +
                    $"?api_key={_apiKey}&query={Uri.EscapeDataString(keyword)}");

                if (!resp.IsSuccessStatusCode) continue;

                var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
                var first = json.GetProperty("results")
                    .EnumerateArray()
                    .FirstOrDefault();

                if (first.ValueKind != JsonValueKind.Undefined)
                    keywordIds.Add(first.GetProperty("id").GetInt32());
            }

            if (!keywordIds.Any()) return [];

            var keywordParam = string.Join(",", keywordIds);

            return await FetchIdsFromEndpointAsync(
                $"discover/movie" +
                $"?with_keywords={keywordParam}" +
                $"&vote_average.gte=6.0" +
                $"&vote_count.gte=100" +
                $"&sort_by=vote_average.desc"
            );
        }

        // ─── Par époque ───────────────────────────────────────────────

        private async Task<List<int>> FetchByEraAsync(
            int yearFrom, int yearTo, string? topGenre)
        {
            var genreParam = "";
            if (topGenre is not null)
            {
                var genreMap = await GetGenreMapAsync();
                if (genreMap.TryGetValue(topGenre, out var genreId))
                    genreParam = $"&with_genres={genreId}";
            }

            return await FetchIdsFromEndpointAsync(
                $"discover/movie" +
                $"?primary_release_date.gte={yearFrom}-01-01" +
                $"&primary_release_date.lte={yearTo}-12-31" +
                $"&vote_average.gte=7.0" +
                $"&vote_count.gte=500" +
                $"&sort_by=vote_average.desc" +
                genreParam
            );
        }

        // ─── Helpers ──────────────────────────────────────────────────

        private async Task<List<int>> FetchIdsFromEndpointAsync(string endpoint)
        {
            var separator = endpoint.Contains('?') ? "&" : "?";
            var response = await _http.GetAsync(
                $"https://api.themoviedb.org/3/{endpoint}" +
                $"{separator}api_key={_apiKey}&language=fr-FR&page=1");

            if (!response.IsSuccessStatusCode) return [];

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            if (!json.TryGetProperty("results", out var results)) return [];

            return results.EnumerateArray()
                .Select(r => r.GetProperty("id").GetInt32())
                .ToList();
        }

        private async Task<Dictionary<string, int>> GetGenreMapAsync()
        {
            var response = await _http.GetAsync(
                $"https://api.themoviedb.org/3/genre/movie/list" +
                $"?api_key={_apiKey}&language=fr-FR");

            if (!response.IsSuccessStatusCode) return [];

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            return json.GetProperty("genres")
                .EnumerateArray()
                .ToDictionary(
                    g => g.GetProperty("name").GetString()!,
                    g => g.GetProperty("id").GetInt32()
                );
        }
        // Ajoute cette méthode dans PersonalizedDiscoveryService.cs
        public async Task DiscoverForAllUsersAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var userIds = await db.Users
                .Select(u => u.Id)
                .ToListAsync();

            foreach (var userId in userIds)
            {
                try
                {
                    await DiscoverForUserAsync(userId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Discovery] Erreur user {userId}: {ex.Message}");
                }
            }
        }
    }
}