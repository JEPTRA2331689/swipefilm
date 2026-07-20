// swipefilm/Auth/PersonalizedDiscoveryService.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using swipefilm.Auth.swipefilm.Services;
using swipefilm.Data;
using swipefilm.Models;

namespace swipefilm.Auth
{
    public class PersonalizedDiscoveryService
    {
        private readonly AppDbContext _db;
        private readonly HttpClient _tmdb;
        private readonly string _apiKey;
        private readonly TmdbService _enrichment;


        // Mapping genres texte → ID TMDB
        private static readonly Dictionary<string, int> GenreIdMap = new()
        {
            ["Action"] = 28,
            ["Adventure"] = 12,
            ["Animation"] = 16,
            ["Comedy"] = 35,
            ["Crime"] = 80,
            ["Documentary"] = 99,
            ["Drama"] = 18,
            ["Family"] = 10751,
            ["Fantasy"] = 14,
            ["History"] = 36,
            ["Horror"] = 27,
            ["Music"] = 10402,
            ["Mystery"] = 9648,
            ["Romance"] = 10749,
            ["Science Fiction"] = 878,
            ["Thriller"] = 53,
            ["War"] = 10752,
            ["Western"] = 37,
            // Français
            ["Aventure"] = 12,
            ["Animation"] = 16,
            ["Comédie"] = 35,
            ["Documentaire"] = 99,
            ["Drame"] = 18,
            ["Famille"] = 10751,
            ["Fantastique"] = 14,
            ["Histoire"] = 36,
            ["Horreur"] = 27,
            ["Musique"] = 10402,
            ["Mystère"] = 9648,
            ["Romance"] = 10749,
            ["Science-fiction"] = 878,
        };

        // Mapping genres texte → ID TMDB — liste TV, différente de la liste films
        // (ex: pas de "Action"/"Adventure" séparés, "Action & Adventure" combiné)
        private static readonly Dictionary<string, int> TvGenreIdMap = new()
        {
            ["Action & Aventure"] = 10759,
            ["Animation"] = 16,
            ["Comédie"] = 35,
            ["Crime"] = 80,
            ["Documentaire"] = 99,
            ["Drame"] = 18,
            ["Familiale"] = 10751,
            ["Enfants"] = 10762,
            ["Mystère"] = 9648,
            ["Actualités"] = 10763,
            ["Réalité"] = 10764,
            ["Science-Fiction & Fantastique"] = 10765,
            ["Feuilleton"] = 10766,
            ["Talk-show"] = 10767,
            ["Guerre & Politique"] = 10768,
            ["Western"] = 37,
        };

        public PersonalizedDiscoveryService(
            AppDbContext db,
            IHttpClientFactory httpFactory,
            IConfiguration config,
            TmdbService enrichment)
        {
            _db = db;
            _tmdb = httpFactory.CreateClient("Tmdb");
            _apiKey = config["Tmdb:ApiKey"]!;
            _enrichment = enrichment;
        }

        // ─── Point d'entrée principal ─────────────────────────────

        public async Task DiscoverForUserAsync(Guid userId)
        {
            var section = SectionProfile.RecentReleases; // section ciblée pour les sorties récentes
            Console.WriteLine($"[Discovery] Début pour userId={userId}");

            var profile = await _db.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile is null)
            {
                Console.WriteLine("[Discovery] Profil introuvable — abandon");
                return;
            }

            // Charge les TmdbIds déjà en BD pour déduplication
            var existingTmdbIds = (await _db.Movies
                .Select(m => m.TmdbId)
                .ToListAsync())
                .ToHashSet();

            var discovered = new List<int>(); // TmdbIds à ajouter

            // ── Source 1 — Films similaires aux swipes droits ─────
            discovered.AddRange(
                await DiscoverFromLikedMoviesAsync(userId, existingTmdbIds));

            // ── Source 2 — Discover par genres préférés ───────────
            discovered.AddRange(
                await DiscoverByGenresAsync(profile, existingTmdbIds));

            // ── Source 3 — Discover par langue préférée ───────────
            discovered.AddRange(
                await DiscoverByLanguageAsync(profile, existingTmdbIds));

            // ── Source 4 — Discover par décennie préférée ─────────
            discovered.AddRange(
                await DiscoverByDecadeAsync(profile, existingTmdbIds));
            discovered.AddRange(
                await DiscoverRecentReleasesAsync(userId, existingTmdbIds, profile, section));

            await DiscoverSeriesForUserAsync(userId);
            // Déduplique et exclut ce qui est déjà en BD
            var toAdd = discovered
                .Distinct()
                .Where(id => !existingTmdbIds.Contains(id))
                .ToList();

            Console.WriteLine($"[Discovery] {toAdd.Count} nouveaux films à ajouter");

            if (!toAdd.Any()) return;

            // ── Ajoute en BD avec CachedAt = MinValue ─────────────
            // (seront enrichis par TmdbEnrichmentService)
            var newMovies = toAdd.Select(tmdbId => new Movie
            {
                Id = Guid.NewGuid(),
                TmdbId = tmdbId,
                Title = $"Film #{tmdbId}", // titre temporaire
                CachedAt = DateTime.MinValue,  // ← sera enrichi
                ContentType = "movie",
            }).ToList();

            _db.Movies.AddRange(newMovies);
            await _db.SaveChangesAsync();

            Console.WriteLine($"[Discovery] {newMovies.Count} films ajoutés en BD");

            // ── Enrichit immédiatement via TMDB ───────────────────
            await _enrichment.EnrichAllMoviesAsync();

            Console.WriteLine("[Discovery] Enrichissement terminé ✅");

            // ── Même principe pour les séries ─────────────────────
            await DiscoverSeriesForUserAsync(userId);
        }

        // ─── Point d'entrée séries — même principe, table Series ──────

        public async Task DiscoverSeriesForUserAsync(Guid userId)
        {
            Console.WriteLine($"[Discovery] Séries — début pour userId={userId}");

            var profile = await _db.UserSeriesProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile is null)
            {
                Console.WriteLine("[Discovery] Séries — profil introuvable, abandon");
                return;
            }

            var existingTmdbIds = (await _db.Series
                .Select(s => s.TmdbId)
                .ToListAsync())
                .ToHashSet();

            var discovered = new List<int>();

            discovered.AddRange(
                await DiscoverFromLikedSeriesAsync(userId, existingTmdbIds));

            discovered.AddRange(
                await DiscoverSeriesByGenresAsync(profile, existingTmdbIds));

            discovered.AddRange(
                await DiscoverSeriesByLanguageAsync(profile, existingTmdbIds));

            discovered.AddRange(
                await DiscoverSeriesByDecadeAsync(profile, existingTmdbIds));

            discovered.AddRange(
                await DiscoverRecentSeriesAsync(profile, existingTmdbIds));

            var toAdd = discovered
                .Distinct()
                .Where(id => !existingTmdbIds.Contains(id))
                .ToList();

            Console.WriteLine($"[Discovery] Séries — {toAdd.Count} nouvelles à ajouter");

            if (!toAdd.Any()) return;

            var newSeries = toAdd.Select(tmdbId => new Series
            {
                Id = Guid.NewGuid(),
                TmdbId = tmdbId,
                Title = $"Série #{tmdbId}",
                CachedAt = DateTime.MinValue,
            }).ToList();

            _db.Series.AddRange(newSeries);
            await _db.SaveChangesAsync();

            Console.WriteLine($"[Discovery] Séries — {newSeries.Count} ajoutées en BD");

            await _enrichment.EnrichAllSeriesAsync();

            Console.WriteLine("[Discovery] Séries — enrichissement terminé ✅");
        }

        private async Task<List<int>> FetchTmdbSeriesIdsAsync(
            string endpoint, HashSet<int> existing, int maxPages = 10)
        {
            var result = new List<int>();
            var separator = endpoint.Contains('?') ? "&" : "?";

            for (int page = 1; page <= maxPages; page++)
            {
                try
                {
                    var baseUrl = "https://api.themoviedb.org/3/";
                    var url = $"{baseUrl}{endpoint}{separator}api_key={_apiKey}&language=fr-FR&page={page}";
                    var res = await _tmdb.GetAsync(url);

                    if (!res.IsSuccessStatusCode) break;

                    var json = await res.Content.ReadAsStringAsync();
                    var doc = JsonDocument.Parse(json);

                    if (!doc.RootElement.TryGetProperty("results", out var results)) break;

                    var ids = results.EnumerateArray()
                        .Where(r => r.TryGetProperty("id", out _))
                        .Select(r => r.GetProperty("id").GetInt32())
                        .Where(id => !existing.Contains(id))
                        .ToList();

                    result.AddRange(ids);

                    if (!doc.RootElement.TryGetProperty("total_pages", out var totalPages)
                        || page >= totalPages.GetInt32())
                        break;

                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Discovery] Erreur TMDB séries ({endpoint} p{page}): {ex.Message}");
                    break;
                }
            }

            return result;
        }

        // ─── Source 1 — Recommendations + Similar depuis séries aimées ───

        private async Task<List<int>> DiscoverFromLikedSeriesAsync(
            Guid userId, HashSet<int> existing)
        {
            var recentLikes = await _db.Swipes
                .Include(s => s.Series)
                .Where(s => s.UserId == userId
                         && s.Direction == SwipeDirection.Right
                         && s.Series != null)
                .OrderByDescending(s => s.CreatedAt)
                .Take(5)
                .Select(s => s.Series!.TmdbId)
                .ToListAsync();

            var topRated = await _db.SeriesWatchHistory
                .Include(w => w.SeriesSeason).ThenInclude(s => s.Series)
                .Where(w => w.UserId == userId && w.UserRating.HasValue)
                .OrderByDescending(w => w.UserRating)
                .Select(w => w.SeriesSeason.Series.TmdbId)
                .Distinct()
                .Take(5)
                .ToListAsync();

            var favorites = await _db.SeriesWatchHistory
                .Include(w => w.SeriesSeason).ThenInclude(s => s.Series)
                .Where(w => w.UserId == userId && w.IsFavorite)
                .OrderByDescending(w => w.LastWatchedAt)
                .Select(w => w.SeriesSeason.Series.TmdbId)
                .Distinct()
                .Take(5)
                .ToListAsync();

            var wellWatched = await _db.SeriesWatchHistory
                .Include(w => w.SeriesSeason).ThenInclude(s => s.Series)
                .Where(w => w.UserId == userId && w.SeriesSeason.EpisodeCount > 0)
                .ToListAsync();

            var wellWatchedIds = wellWatched
                .Where(w => (float)w.WatchedEpisodeCount / w.SeriesSeason.EpisodeCount >= 0.85f)
                .OrderByDescending(w => w.LastWatchedAt)
                .Select(w => w.SeriesSeason.Series.TmdbId)
                .Distinct()
                .Take(5)
                .ToList();

            var allReference = recentLikes
                .Concat(topRated)
                .Concat(favorites)
                .Concat(wellWatchedIds)
                .Distinct()
                .ToList();

            Console.WriteLine($"[Discovery] Séries de référence : {allReference.Count} uniques");

            var result = new List<int>();

            foreach (var tmdbId in allReference)
            {
                result.AddRange(await FetchTmdbSeriesIdsAsync(
                    $"tv/{tmdbId}/recommendations", existing, maxPages: 2));

                result.AddRange(await FetchTmdbSeriesIdsAsync(
                    $"tv/{tmdbId}/similar", existing, maxPages: 1));

                await Task.Delay(150);
            }

            Console.WriteLine($"[Discovery] Séries source 1 : {result.Count}");
            return result;
        }

        // ─── Source 2 — Discover séries par genres préférés ───────────────

        private async Task<List<int>> DiscoverSeriesByGenresAsync(
            UserSeriesProfile profile, HashSet<int> existing)
        {
            if (!profile.GenreWeights.Any()) return [];

            var acceptedLangs = profile.OriginalLanguageWeights
                .Where(l => l.Value > 0.2f)
                .OrderByDescending(l => l.Value)
                .Take(3)
                .Select(l => l.Key)
                .ToList();

            if (!acceptedLangs.Any())
                acceptedLangs = ["en", "fr"];

            var langFilter = string.Join("|", acceptedLangs);

            var allGenres = profile.GenreWeights
                .OrderByDescending(g => g.Value)
                .Where(g => TvGenreIdMap.ContainsKey(g.Key))
                .Select(g => TvGenreIdMap[g.Key])
                .ToList();

            if (!allGenres.Any()) return [];

            var result = new List<int>();

            var topGenres = allGenres.Take(3).ToList();
            if (topGenres.Any())
            {
                result.AddRange(await FetchTmdbSeriesIdsAsync(
                    $"discover/tv?with_genres={string.Join(",", topGenres)}&with_original_language={langFilter}&sort_by=vote_average.desc&vote_count.gte=100&vote_average.gte=6.5",
                    existing, maxPages: 5));

                await Task.Delay(150);
            }

            var extraGenres = allGenres.Skip(3).Take(3).ToList();
            foreach (var genreId in extraGenres)
            {
                result.AddRange(await FetchTmdbSeriesIdsAsync(
                    $"discover/tv?with_genres={genreId}&with_original_language={langFilter}&sort_by=vote_average.desc&vote_count.gte=100&vote_average.gte=6.5",
                    existing, maxPages: 10));

                await Task.Delay(150);
            }

            Console.WriteLine($"[Discovery] Séries source 2 (genres+langues={langFilter}): {result.Count}");
            return result;
        }

        // ─── Source 3 — Discover séries par langue préférée ───────────────

        private async Task<List<int>> DiscoverSeriesByLanguageAsync(
            UserSeriesProfile profile, HashSet<int> existing)
        {
            if (!profile.OriginalLanguageWeights.Any()) return [];

            var result = new List<int>();

            var hasNonEnglish = profile.OriginalLanguageWeights.Any(l => l.Key != "en");

            var topLangs = profile.OriginalLanguageWeights
                .OrderByDescending(l => l.Value)
                .Where(l => !hasNonEnglish || l.Key != "en")
                .Take(2)
                .Select(l => l.Key)
                .ToList();

            foreach (var lang in topLangs)
            {
                result.AddRange(await FetchTmdbSeriesIdsAsync(
                    $"discover/tv?with_original_language={lang}&sort_by=vote_average.desc&vote_count.gte=50&vote_average.gte=6.0",
                    existing, maxPages: 20));

                await Task.Delay(150);
            }

            Console.WriteLine($"[Discovery] Séries source 3 (langues={string.Join(",", topLangs)}): {result.Count}");
            return result;
        }

        // ─── Source 4 — Discover séries par décennie préférée ─────────────

        private async Task<List<int>> DiscoverSeriesByDecadeAsync(
            UserSeriesProfile profile, HashSet<int> existing)
        {
            if (!profile.PreferredDecadeWeights.Any()) return [];

            var topDecades = profile.PreferredDecadeWeights
                .OrderByDescending(d => d.Value)
                .Take(3)
                .Select(d => d.Key)
                .ToList();

            var result = new List<int>();

            foreach (var decade in topDecades)
            {
                if (!int.TryParse(decade, out var decadeYear)) continue;

                var yearFrom = decadeYear;
                var yearTo = decadeYear + 9;

                result.AddRange(await FetchTmdbSeriesIdsAsync(
                    $"discover/tv?first_air_date.gte={yearFrom}-01-01&first_air_date.lte={yearTo}-12-31&sort_by=vote_average.desc&vote_count.gte=100&vote_average.gte=7.0",
                    existing, maxPages: 4));

                await Task.Delay(150);
            }

            Console.WriteLine($"[Discovery] Séries source 4 (décennies): {result.Count}");
            return result;
        }

        // ─── Sorties de séries récentes ────────────────────────────────────

        private async Task<List<int>> DiscoverRecentSeriesAsync(
            UserSeriesProfile profile, HashSet<int> existing)
        {
            const int months = 6;
            var dateFrom = DateTime.UtcNow.AddMonths(-months).ToString("yyyy-MM-dd");
            var dateTo = DateTime.UtcNow.ToString("yyyy-MM-dd");

            var topGenres = profile.GenreWeights
                .Where(g => g.Value > 0 && TvGenreIdMap.ContainsKey(g.Key))
                .OrderByDescending(g => g.Value)
                .Take(3)
                .Select(g => TvGenreIdMap[g.Key])
                .ToList();

            var result = new List<int>();

            if (topGenres.Any())
            {
                var genreParam = string.Join(",", topGenres);
                result.AddRange(await FetchTmdbSeriesIdsAsync(
                    $"discover/tv?with_genres={genreParam}" +
                    $"&first_air_date.gte={dateFrom}" +
                    $"&first_air_date.lte={dateTo}" +
                    $"&sort_by=popularity.desc" +
                    $"&vote_count.gte=20",
                    existing, maxPages: 6));

                Console.WriteLine($"[Discovery] Séries récentes: {result}");

                await Task.Delay(150);
            }

            result.AddRange(await FetchTmdbSeriesIdsAsync(
                $"discover/tv" +
                $"?first_air_date.gte={dateFrom}" +
                $"&first_air_date.lte={dateTo}" +
                $"&sort_by=popularity.desc" +
                $"&vote_count.gte=50" +
                $"&vote_average.gte=6.0",
                existing, maxPages: 8));

            Console.WriteLine($"[Discovery] Séries récentes: {result.Count}");
            return result;
        }


        private async Task<List<int>> FetchTmdbMovieIdsAsync(
            string endpoint, HashSet<int> existing, int maxPages = 10)
        {
            var result = new List<int>();
            var separator = endpoint.Contains('?') ? "&" : "?";

            for (int page = 1; page <= maxPages; page++)
            {
                try
                {
                    var baseUrl = "https://api.themoviedb.org/3/";
                    var url = $"{baseUrl}{endpoint}{separator}api_key={_apiKey}&language=fr-FR&page={page}";
                    var res = await _tmdb.GetAsync(url);

                    if (!res.IsSuccessStatusCode) break;

                    var json = await res.Content.ReadAsStringAsync();
                    var doc = JsonDocument.Parse(json);

                    if (!doc.RootElement.TryGetProperty("results", out var results)) break;

                    var ids = results.EnumerateArray()
                        .Where(r => r.TryGetProperty("id", out _)
                                 && r.TryGetProperty("media_type", out var mt)
                                    ? mt.GetString() != "tv"  // exclut les séries
                                    : true)
                        .Select(r => r.GetProperty("id").GetInt32())
                        .Where(id => !existing.Contains(id))
                        .ToList();

                    result.AddRange(ids);

                    // Vérifie s'il y a d'autres pages
                    if (!doc.RootElement.TryGetProperty("total_pages", out var totalPages)
                        || page >= totalPages.GetInt32())
                        break;

                    await Task.Delay(100); // rate limit
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Discovery] Erreur TMDB ({endpoint} p{page}): {ex.Message}");
                    break;
                }
            }

            return result;
        }
        // ─── Source 1 — Recommendations + Similar depuis films aimés ─────

        private async Task<List<int>> DiscoverFromLikedMoviesAsync(
            Guid userId, HashSet<int> existing)
        {
            // ── 1. Derniers swipes droits (5 au lieu de 15) ───────────
            var recentLikes = await _db.Swipes
                .Include(s => s.Movie)
                .Where(s => s.UserId == userId
                         && s.Direction == SwipeDirection.Right
                         && s.Movie != null)
                .OrderByDescending(s => s.CreatedAt)
                .Take(5)
                .Select(s => s.Movie!.TmdbId)
                .ToListAsync();

            // ── 2. Films les mieux notés dans l'historique (5) ────────
            var topRated = await _db.WatchHistory
                .Include(w => w.Movie)
                .Where(w => w.UserId == userId
                         && w.Movie != null
                         && w.UserRating.HasValue)
                .OrderByDescending(w => w.UserRating)
                .Take(5)
                .Select(w => w.Movie!.TmdbId)
                .ToListAsync();

            // ── 3. Films favoris (5) ──────────────────────────────────
            var favorites = await _db.WatchHistory
                .Include(w => w.Movie)
                .Where(w => w.UserId == userId
                         && w.Movie != null
                         && w.IsFavorite)
                .OrderByDescending(w => w.LastWatchedAt)
                .Take(5)
                .Select(w => w.Movie!.TmdbId)
                .ToListAsync();

            // ── 4. Films bien regardés sans note explicite (5) ────────
            // Fallback si pas assez de notes/favoris
            var wellWatched = await _db.WatchHistory
                .Include(w => w.Movie)
                .Where(w => w.UserId == userId
                         && w.Movie != null
                         && w.Movie.RuntimeMinutes > 0)
                .ToListAsync();

            var wellWatchedIds = wellWatched
                .Where(w => w.Movie!.RuntimeMinutes > 0 &&
                    (float)w.WatchDurationSec /
                    (w.Movie!.RuntimeMinutes!.Value * 60) >= 0.85f)
                .OrderByDescending(w => w.LastWatchedAt)
                .Take(5)
                .Select(w => w.Movie!.TmdbId)
                .ToList();

            // ── Combine et déduplique les sources ─────────────────────
            var allReference = recentLikes
                .Concat(topRated)
                .Concat(favorites)
                .Concat(wellWatchedIds)
                .Distinct()
                .ToList();

            Console.WriteLine($"[Discovery] Films de référence : " +
                $"{recentLikes.Count} récents, " +
                $"{topRated.Count} mieux notés, " +
                $"{favorites.Count} favoris, " +
                $"{wellWatchedIds.Count} bien regardés " +
                $"→ {allReference.Count} uniques");

            // ── Fetch recommendations + similar pour chaque ───────────
            var result = new List<int>();

            foreach (var tmdbId in allReference)
            {
                result.AddRange(await FetchTmdbMovieIdsAsync(
                    $"movie/{tmdbId}/recommendations", existing, maxPages: 2));

                result.AddRange(await FetchTmdbMovieIdsAsync(
                    $"movie/{tmdbId}/similar", existing, maxPages: 1));

                await Task.Delay(150);
            }

            Console.WriteLine($"[Discovery] Source 1 (diversifiée): {result.Count} films");
            return result;
        }

        // ─── Source 2 — Discover par genres préférés ──────────────────────



        private async Task<List<int>> DiscoverByGenresAsync(
        UserProfile profile, HashSet<int> existing)
            {
                if (!profile.GenreWeights.Any()) return [];

                // ✅ Langues acceptées — top 3 avec score > 0.2
                var acceptedLangs = profile.OriginalLanguageWeights
                    .Where(l => l.Value > 0.2f)
                    .OrderByDescending(l => l.Value)
                    .Take(3)
                    .Select(l => l.Key)
                    .ToList();

                // Fallback si profil trop neuf
                if (!acceptedLangs.Any())
                    acceptedLangs = ["en", "fr"];

                var langFilter = string.Join("|", acceptedLangs); // "de|en|fr|zh"

                var allGenres = profile.GenreWeights
                    .OrderByDescending(g => g.Value)
                    .Where(g => GenreIdMap.ContainsKey(g.Key))
                    .Select(g => GenreIdMap[g.Key])
                    .ToList();

                if (!allGenres.Any()) return [];

                var result = new List<int>();

                // Top 3 genres ensemble
                var topGenres = allGenres.Take(3).ToList();
                if (topGenres.Any())
                {
                    // ✅ with_original_language filtre les langues
                    result.AddRange(await FetchTmdbMovieIdsAsync(
                        $"discover/movie?with_genres={string.Join(",", topGenres)}&with_original_language={langFilter}&sort_by=vote_average.desc&vote_count.gte=100&vote_average.gte=6.5",
                        existing, maxPages: 5));

                    await Task.Delay(150);
                }

                // Genres 4-6 séparément
                var extraGenres = allGenres.Skip(3).Take(3).ToList();
                foreach (var genreId in extraGenres)
                {
                    result.AddRange(await FetchTmdbMovieIdsAsync(
                        $"discover/movie?with_genres={genreId}&with_original_language={langFilter}&sort_by=vote_average.desc&vote_count.gte=100&vote_average.gte=6.5",
                        existing, maxPages: 10));

                    await Task.Delay(150);
                }

                Console.WriteLine($"[Discovery] Source 2 (genres+langues={langFilter}): {result.Count} films");
                return result;
        }

        // ─── Source 3 — Discover par langue préférée ──────────────────────

        private async Task<List<int>> DiscoverByLanguageAsync(
            UserProfile profile, HashSet<int> existing)
        {
            if (!profile.OriginalLanguageWeights.Any()) return [];

            var result = new List<int>();

            // ✅ Top 2 langues (skip "en" seulement si d'autres existent)
            var hasNonEnglish = profile.OriginalLanguageWeights
                .Any(l => l.Key != "en");

            var topLangs = profile.OriginalLanguageWeights
                .OrderByDescending(l => l.Value)
                .Where(l => !hasNonEnglish || l.Key != "en")
                .Take(2)
                .Select(l => l.Key)
                .ToList();

            foreach (var lang in topLangs)
            {
                // ✅ 2 pages par langue (40 films chacune)
                result.AddRange(await FetchTmdbMovieIdsAsync(
                    $"discover/movie?with_original_language={lang}&sort_by=vote_average.desc&vote_count.gte=50&vote_average.gte=6.0",
                    existing, maxPages: 20));

                await Task.Delay(150);
            }

            Console.WriteLine($"[Discovery] Source 3 (langues={string.Join(",", topLangs)}): {result.Count} films");
            return result;
        }

        // ─── Source 4 — Discover par décennie préférée ────────────────────

        private async Task<List<int>> DiscoverByDecadeAsync(
            UserProfile profile, HashSet<int> existing)
        {
            if (!profile.PreferredDecadeWeights.Any()) return [];

            // ✅ Top 3 décennies au lieu de 2
            var topDecades = profile.PreferredDecadeWeights
                .OrderByDescending(d => d.Value)
                .Take(3)
                .Select(d => d.Key)
                .ToList();

            var result = new List<int>();

            foreach (var decade in topDecades)
            {
                if (!int.TryParse(decade, out var decadeYear)) continue;

                var yearFrom = decadeYear;
                var yearTo = decadeYear + 9;

                // ✅ 2 pages par décennie (40 films chacune)
                result.AddRange(await FetchTmdbMovieIdsAsync(
                    $"discover/movie?primary_release_date.gte={yearFrom}-01-01&primary_release_date.lte={yearTo}-12-31&sort_by=vote_average.desc&vote_count.gte=100&vote_average.gte=7.0",
                    existing, maxPages: 4));

                await Task.Delay(150);
            }

            Console.WriteLine($"[Discovery] Source 4 (décennies): {result.Count} films");
            return result;
        }
        private async Task<List<int>> DiscoverRecentReleasesAsync(
        Guid userId, HashSet<int> existing, UserProfile profile, SectionProfile section)
        {
            var recent= section.ReleasedWithinMonths ?? 6; // fallback 6 mois si pas défini

            var dateFrom = DateTime.UtcNow.AddMonths(-recent).ToString("yyyy-MM-dd");
            var dateTo = DateTime.UtcNow.ToString("yyyy-MM-dd");

            // ✅ Top 3 genres du profil → films récents dans ces genres
            var topGenres = profile.GenreWeights
                .Where(g => g.Value > 0 && GenreIdMap.ContainsKey(g.Key))
                .OrderByDescending(g => g.Value)
                .Take(3)
                .Select(g => GenreIdMap[g.Key])
                .ToList();

            var result = new List<int>();

            if (topGenres.Any())
            {
                var genreParam = string.Join(",", topGenres);
                result.AddRange(await FetchTmdbMovieIdsAsync(
                    $"discover/movie?with_genres={genreParam}" +
                    $"&primary_release_date.gte={dateFrom}" +
                    $"&primary_release_date.lte={dateTo}" +
                    $"&sort_by=popularity.desc" +
                    $"&vote_count.gte=20",
                    existing, maxPages: 6));

                await Task.Delay(150);
            }

            // ✅ Films récents populaires sans filtre genre (découverte large)
            result.AddRange(await FetchTmdbMovieIdsAsync(
                $"discover/movie" +
                $"?primary_release_date.gte={dateFrom}" +
                $"&primary_release_date.lte={dateTo}" +
                $"&sort_by=popularity.desc" +
                $"&vote_count.gte=50" +
                $"&vote_average.gte=6.0",
                existing, maxPages: 8));

            // ✅ Langue préférée + récent
            var topLang = profile.OriginalLanguageWeights
                .Where(l => l.Value > 0.1f )
                .OrderByDescending(l => l.Value)
                .Select(l => l.Key)
                .FirstOrDefault();

            if (topLang != null)
            {
                result.AddRange(await FetchTmdbMovieIdsAsync(
                    $"discover/movie" +
                    $"?with_original_language={topLang}" +
                    $"&primary_release_date.gte={dateFrom}" +
                    $"&primary_release_date.lte={dateTo}" +
                    $"&sort_by=popularity.desc" +
                    $"&vote_count.gte=10",
                    existing, maxPages: 3));
            }

            Console.WriteLine($"[Discovery] Sorties récentes: {result.Count} films");
            return result;
        }
    }
}