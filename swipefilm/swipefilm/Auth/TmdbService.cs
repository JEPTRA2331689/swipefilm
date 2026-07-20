namespace swipefilm.Auth
{
    // swipefilm/Services/TmdbService.cs
    using System.Text.Json;
    using global::swipefilm.Models;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Configuration;


    namespace swipefilm.Services
    {
        public class TmdbService
        {
            private readonly HttpClient _http;
            private readonly AppDbContext _db;
            private readonly IMemoryCache _cache;
            private readonly string _apiKey;

            public TmdbService(
                IHttpClientFactory httpClientFactory,
                AppDbContext db,
                IMemoryCache cache,
                IConfiguration config)
            {
                _http = httpClientFactory.CreateClient("Tmdb");
                _db = db;
                _cache = cache;
                _apiKey = config["Tmdb:ApiKey"]
                    ?? throw new InvalidOperationException("Tmdb:ApiKey manquant");
            }

            // ─── Cartes légères (feed/recommandations) ─────────────────────
            // Contrairement à GetMovieDetailsAsync/GetSeriesDetailsAsync (page
            // détail, un seul appel), celles-ci sont appelées ~100x par page
            // d'accueil chargée — pas d'append_to_response (credits/keywords
            // inutiles ici), et un cache court en mémoire pour ne pas refaire
            // le même appel TMDB à chaque utilisateur qui voit le même titre.

            public async Task<MovieCardDto?> GetMovieCardAsync(int tmdbId, string locale)
            {
                var cacheKey = $"moviecard:{tmdbId}:{locale}";
                if (_cache.TryGetValue(cacheKey, out MovieCardDto? cached)) return cached;

                try
                {
                    var response = await _http.GetAsync(
                        $"https://api.themoviedb.org/3/movie/{tmdbId}" +
                        $"?api_key={_apiKey}&language={locale}");

                    if (!response.IsSuccessStatusCode) return null;

                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();

                    var card = new MovieCardDto(
                        Title: json.GetProperty("title").GetString() ?? "",
                        PosterPath: json.TryGetProperty("poster_path", out var pp) && pp.ValueKind != JsonValueKind.Null
                            ? pp.GetString() : null,
                        BackdropPath: json.TryGetProperty("backdrop_path", out var bp) && bp.ValueKind != JsonValueKind.Null
                            ? bp.GetString() : null,
                        Overview: json.TryGetProperty("overview", out var ov) ? ov.GetString() : null
                    );

                    _cache.Set(cacheKey, card, TimeSpan.FromHours(6));
                    return card;
                }
                catch { return null; }
            }

            public async Task<MovieCardDto?> GetSeriesCardAsync(int tmdbId, string locale)
            {
                var cacheKey = $"seriescard:{tmdbId}:{locale}";
                if (_cache.TryGetValue(cacheKey, out MovieCardDto? cached)) return cached;

                try
                {
                    var response = await _http.GetAsync(
                        $"https://api.themoviedb.org/3/tv/{tmdbId}" +
                        $"?api_key={_apiKey}&language={locale}");

                    if (!response.IsSuccessStatusCode) return null;

                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();

                    var card = new MovieCardDto(
                        Title: json.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                        PosterPath: json.TryGetProperty("poster_path", out var pp) && pp.ValueKind != JsonValueKind.Null
                            ? pp.GetString() : null,
                        BackdropPath: json.TryGetProperty("backdrop_path", out var bp) && bp.ValueKind != JsonValueKind.Null
                            ? bp.GetString() : null,
                        Overview: json.TryGetProperty("overview", out var ov) ? ov.GetString() : null
                    );

                    _cache.Set(cacheKey, card, TimeSpan.FromHours(6));
                    return card;
                }
                catch { return null; }
            }

            // ─── Bande-annonce — appelée seulement au survol (4s), jamais en
            // masse pour tout un carrousel/toute une page. Cache long (les
            // bandes-annonces ne changent quasiment jamais).

            public async Task<string?> GetMovieTrailerAsync(int tmdbId)
                => await GetTrailerAsync("movie", tmdbId);

            public async Task<string?> GetSeriesTrailerAsync(int tmdbId)
                => await GetTrailerAsync("tv", tmdbId);

            private async Task<string?> GetTrailerAsync(string endpointType, int tmdbId)
            {
                var cacheKey = $"trailer:{endpointType}:{tmdbId}";
                if (_cache.TryGetValue(cacheKey, out string? cached)) return cached;

                try
                {
                    var response = await _http.GetAsync(
                        $"https://api.themoviedb.org/3/{endpointType}/{tmdbId}/videos" +
                        $"?api_key={_apiKey}");

                    if (!response.IsSuccessStatusCode) return null;

                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                    if (!json.TryGetProperty("results", out var results)) return null;

                    var videos = results.EnumerateArray()
                        .Where(v => v.TryGetProperty("site", out var site)
                                 && site.GetString() == "YouTube")
                        .ToList();

                    string? Key(JsonElement v) =>
                        v.ValueKind == JsonValueKind.Undefined
                            ? null
                            : v.TryGetProperty("key", out var k) ? k.GetString() : null;

                    // Priorité : Trailer officiel > Trailer > Teaser
                    var best =
                        Key(videos.FirstOrDefault(v =>
                            v.TryGetProperty("type", out var t) && t.GetString() == "Trailer"
                            && v.TryGetProperty("official", out var o) && o.GetBoolean()))
                        ?? Key(videos.FirstOrDefault(v =>
                            v.TryGetProperty("type", out var t) && t.GetString() == "Trailer"))
                        ?? Key(videos.FirstOrDefault(v =>
                            v.TryGetProperty("type", out var t) && t.GetString() == "Teaser"));

                    _cache.Set(cacheKey, best, TimeSpan.FromHours(24));
                    return best;
                }
                catch { return null; }
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
                    $"&append_to_response=credits,keywords,external_ids"
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
                    ? pp.GetString() : null;

                movie.BackdropPath = json.TryGetProperty("backdrop_path", out var bp)
                    ? bp.GetString() : null;

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

                // Note et popularité
                if (json.TryGetProperty("vote_count", out var vc))
                    movie.TmdbVoteCount = vc.GetInt32();

                if (json.TryGetProperty("popularity", out var pop))
                    movie.TmdbPopularity = pop.GetSingle();

                // Genres
                if (json.TryGetProperty("genres", out var genres))
                    movie.Genres = genres.EnumerateArray()
                        .Select(g => g.GetProperty("name").GetString()!)
                        .Where(g => g is not null)
                        .ToArray();
                if (json.TryGetProperty("original_language", out var originalLanguage))
                    movie.OriginalLanguage = originalLanguage.GetString();

                // TvdbId (séries uniquement — récupéré dans le même appel via
                // append_to_response=external_ids, pas d'aller-retour séparé
                // au moment de la demande Sonarr, cf. Overseerr/Jellyseerr)
                if (movie.ContentType != "movie"
                    && json.TryGetProperty("external_ids", out var externalIds)
                    && externalIds.TryGetProperty("tvdb_id", out var tvdbId)
                    && tvdbId.ValueKind == JsonValueKind.Number)
                    movie.TvdbId = tvdbId.GetInt32();

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

            // ─── Enrichir toutes les séries sans métadonnées ──────────────

            public async Task EnrichAllSeriesAsync()
            {
                var series = await _db.Series
                    .Where(s => s.CachedAt == DateTime.MinValue
                             || s.CachedAt < DateTime.UtcNow.AddDays(-7))
                    .ToListAsync();

                foreach (var s in series)
                {
                    try
                    {
                        await EnrichSeriesAsync(s);
                        await Task.Delay(25);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Erreur TMDB pour {s.Title}: {ex.Message}");
                    }
                }
            }

            // ─── Enrichir une série spécifique ─────────────────────────────

            public async Task EnrichSeriesAsync(Series series)
            {
                var response = await _http.GetAsync(
                    $"https://api.themoviedb.org/3/tv/{series.TmdbId}" +
                    $"?api_key={_apiKey}" +
                    $"&language=fr-FR" +
                    $"&append_to_response=credits,keywords,external_ids"
                );

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"TMDB {response.StatusCode} pour Series TmdbId {series.TmdbId}");
                    return;
                }

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();

                series.Title = json.TryGetProperty("name", out var name)
                    ? name.GetString() ?? series.Title : series.Title;

                series.OriginalTitle = json.TryGetProperty("original_name", out var on)
                    ? on.GetString() : null;

                series.Overview = json.TryGetProperty("overview", out var ov)
                    ? ov.GetString() : null;

                series.PosterPath = json.TryGetProperty("poster_path", out var pp)
                    ? pp.GetString() : null;

                series.BackdropPath = json.TryGetProperty("backdrop_path", out var bp)
                    ? bp.GetString() : null;

                if (json.TryGetProperty("first_air_date", out var fd)
                    && DateOnly.TryParse(fd.GetString(), out var date))
                    series.FirstAirDate = date;

                if (json.TryGetProperty("vote_average", out var va))
                    series.TmdbRating = va.GetSingle();

                if (json.TryGetProperty("vote_count", out var vc))
                    series.TmdbVoteCount = vc.GetInt32();

                if (json.TryGetProperty("popularity", out var pop))
                    series.TmdbPopularity = pop.GetSingle();

                if (json.TryGetProperty("number_of_seasons", out var ns))
                    series.NumberOfSeasons = ns.GetInt32();

                if (json.TryGetProperty("number_of_episodes", out var ne))
                    series.NumberOfEpisodes = ne.GetInt32();

                if (json.TryGetProperty("genres", out var genres))
                    series.Genres = genres.EnumerateArray()
                        .Select(g => g.GetProperty("name").GetString()!)
                        .Where(g => g is not null)
                        .ToArray();

                if (json.TryGetProperty("original_language", out var originalLanguage))
                    series.OriginalLanguage = originalLanguage.GetString();

                if (json.TryGetProperty("external_ids", out var externalIds)
                    && externalIds.TryGetProperty("tvdb_id", out var tvdbId)
                    && tvdbId.ValueKind == JsonValueKind.Number)
                    series.TvdbId = tvdbId.GetInt32();

                // Créateurs — les séries n'ont pas de "Director" fiable au niveau
                // show, TMDB expose created_by séparément.
                if (json.TryGetProperty("created_by", out var createdBy))
                    series.CreatedBy = createdBy.EnumerateArray()
                        .Select(c => c.GetProperty("name").GetString()!)
                        .Where(c => c is not null)
                        .ToArray();

                if (json.TryGetProperty("keywords", out var kw)
                    && kw.TryGetProperty("results", out var kwResults))
                    series.Keywords = kwResults.EnumerateArray()
                        .Select(k => k.GetProperty("name").GetString()!)
                        .Where(k => k is not null)
                        .Take(10)
                        .ToArray();

                if (json.TryGetProperty("credits", out var credits)
                    && credits.TryGetProperty("cast", out var cast))
                    series.CastTop5 = cast.EnumerateArray()
                        .Take(5)
                        .Select(c => c.GetProperty("name").GetString()!)
                        .Where(c => c is not null)
                        .ToArray();

                series.CachedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            // ─── Détail film — live, paramétré par région/langue utilisateur ─
            // Contrairement à EnrichMovieAsync (cache DB, sert au scoring,
            // langue fixe), ceci n'est jamais stocké : appelé à chaque
            // ouverture de fiche détail, avec la langue/région du user connecté.

            public async Task<MovieDetailDto?> GetMovieDetailsAsync(
                int tmdbId, string locale, string region)
            {
                var response = await _http.GetAsync(
                    $"https://api.themoviedb.org/3/movie/{tmdbId}" +
                    $"?api_key={_apiKey}" +
                    $"&language={locale}" +
                    $"&append_to_response=credits,release_dates");

                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();

                string? certification = null;
                if (json.TryGetProperty("release_dates", out var rd)
                    && rd.TryGetProperty("results", out var rdResults))
                {
                    foreach (var country in rdResults.EnumerateArray())
                    {
                        if (country.GetProperty("iso_3166_1").GetString() != region) continue;
                        certification = country.GetProperty("release_dates")
                            .EnumerateArray()
                            .Select(d => d.TryGetProperty("certification", out var c) ? c.GetString() : null)
                            .FirstOrDefault(c => !string.IsNullOrEmpty(c));
                        break;
                    }
                }

                var directors = json.TryGetProperty("credits", out var credits)
                    && credits.TryGetProperty("crew", out var crew)
                    ? crew.EnumerateArray()
                        .Where(c => c.GetProperty("job").GetString() == "Director")
                        .Select(c => c.GetProperty("name").GetString()!)
                        .ToArray()
                    : [];

                var cast = credits.ValueKind == JsonValueKind.Object
                    && credits.TryGetProperty("cast", out var castArr)
                    ? castArr.EnumerateArray().Take(10)
                        .Select(c => c.GetProperty("name").GetString()!)
                        .ToArray()
                    : [];

                return new MovieDetailDto(
                    TmdbId: tmdbId,
                    Title: json.GetProperty("title").GetString() ?? "",
                    OriginalTitle: json.TryGetProperty("original_title", out var ot) ? ot.GetString() : null,
                    Overview: json.TryGetProperty("overview", out var ov) ? ov.GetString() : null,
                    PosterPath: json.TryGetProperty("poster_path", out var pp) && pp.ValueKind != JsonValueKind.Null
                        ? pp.GetString() : null,
                    BackdropPath: json.TryGetProperty("backdrop_path", out var bp) && bp.ValueKind != JsonValueKind.Null
                        ? bp.GetString() : null,
                    ReleaseDate: json.TryGetProperty("release_date", out var rdate)
                        && DateOnly.TryParse(rdate.GetString(), out var date) ? date : null,
                    RuntimeMinutes: json.TryGetProperty("runtime", out var rt) && rt.ValueKind == JsonValueKind.Number ? rt.GetInt32() : null,
                    Genres: json.TryGetProperty("genres", out var genres)
                        ? genres.EnumerateArray().Select(g => g.GetProperty("name").GetString()!).ToArray() : [],
                    Certification: certification,
                    Directors: directors,
                    CastTop10: cast,
                    TmdbRating: json.TryGetProperty("vote_average", out var va) ? va.GetSingle() : 0f,
                    TmdbVoteCount: json.TryGetProperty("vote_count", out var vc) ? vc.GetInt32() : 0,
                    TmdbPopularity: json.TryGetProperty("popularity", out var pop) ? pop.GetSingle() : 0f
                );
            }

            // ─── Détail série — même principe que le film, endpoint /tv ───

            public async Task<SeriesDetailDto?> GetSeriesDetailsAsync(
                int tmdbId, string locale, string region)
            {
                var response = await _http.GetAsync(
                    $"https://api.themoviedb.org/3/tv/{tmdbId}" +
                    $"?api_key={_apiKey}" +
                    $"&language={locale}" +
                    $"&append_to_response=credits,content_ratings");

                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();

                string? certification = null;
                if (json.TryGetProperty("content_ratings", out var cr)
                    && cr.TryGetProperty("results", out var crResults))
                {
                    var match = crResults.EnumerateArray()
                        .FirstOrDefault(c => c.GetProperty("iso_3166_1").GetString() == region);

                    if (match.ValueKind == JsonValueKind.Object
                        && match.TryGetProperty("rating", out var rating))
                        certification = rating.GetString();
                }

                var createdBy = json.TryGetProperty("created_by", out var cb)
                    ? cb.EnumerateArray().Select(c => c.GetProperty("name").GetString()!).ToArray() : [];

                var cast = json.TryGetProperty("credits", out var credits)
                    && credits.TryGetProperty("cast", out var castArr)
                    ? castArr.EnumerateArray().Take(10)
                        .Select(c => c.GetProperty("name").GetString()!)
                        .ToArray()
                    : [];

                // ✅ season_number=0 = "Specials" TMDB — exclu, pas une vraie saison
                var seasons = json.TryGetProperty("seasons", out var seasonsArr)
                    ? seasonsArr.EnumerateArray()
                        .Select(s => new TmdbSeasonDto(
                            SeasonNumber: s.GetProperty("season_number").GetInt32(),
                            EpisodeCount: s.TryGetProperty("episode_count", out var ec) ? ec.GetInt32() : 0,
                            Name: s.TryGetProperty("name", out var sn) ? sn.GetString() : null,
                            PosterPath: s.TryGetProperty("poster_path", out var sp) && sp.ValueKind != JsonValueKind.Null
                                ? sp.GetString() : null
                        ))
                        .Where(s => s.SeasonNumber > 0)
                        .OrderBy(s => s.SeasonNumber)
                        .ToArray()
                    : [];

                return new SeriesDetailDto(
                    TmdbId: tmdbId,
                    Title: json.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "",
                    OriginalTitle: json.TryGetProperty("original_name", out var on) ? on.GetString() : null,
                    Overview: json.TryGetProperty("overview", out var ov) ? ov.GetString() : null,
                    PosterPath: json.TryGetProperty("poster_path", out var pp) && pp.ValueKind != JsonValueKind.Null
                        ? pp.GetString() : null,
                    BackdropPath: json.TryGetProperty("backdrop_path", out var bp) && bp.ValueKind != JsonValueKind.Null
                        ? bp.GetString() : null,
                    FirstAirDate: json.TryGetProperty("first_air_date", out var fd)
                        && DateOnly.TryParse(fd.GetString(), out var date) ? date : null,
                    NumberOfSeasons: json.TryGetProperty("number_of_seasons", out var ns) ? ns.GetInt32() : 0,
                    NumberOfEpisodes: json.TryGetProperty("number_of_episodes", out var ne) ? ne.GetInt32() : 0,
                    Genres: json.TryGetProperty("genres", out var genres)
                        ? genres.EnumerateArray().Select(g => g.GetProperty("name").GetString()!).ToArray() : [],
                    Certification: certification,
                    CreatedBy: createdBy,
                    CastTop10: cast,
                    TmdbRating: json.TryGetProperty("vote_average", out var va) ? va.GetSingle() : 0f,
                    TmdbVoteCount: json.TryGetProperty("vote_count", out var vc) ? vc.GetInt32() : 0,
                    TmdbPopularity: json.TryGetProperty("popularity", out var pop) ? pop.GetSingle() : 0f,
                    Seasons: seasons
                );
            }

            // ─── Résoudre le TvdbId d'une série (requis par Sonarr) ───────
            // TMDB et TheTVDB sont deux espaces d'ID différents : Sonarr attend
            // un tvdbId, jamais un tmdbId.

            public async Task<int?> GetTvdbIdAsync(int tmdbId)
            {
                var response = await _http.GetAsync(
                    $"https://api.themoviedb.org/3/tv/{tmdbId}/external_ids" +
                    $"?api_key={_apiKey}");

                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                return json.TryGetProperty("tvdb_id", out var tvdb)
                    && tvdb.ValueKind == JsonValueKind.Number
                    ? tvdb.GetInt32() : null;
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

            // ─── Recherche utilisateur (page /search) ────────────────────
            // /search/multi renvoie films + séries + personnes en un appel —
            // on ne garde que movie/tv, les personnes ne sont pas pertinentes ici.

            public async Task<TmdbSearchResultDto?> SearchMultiAsync(string query, string locale, int page)
            {
                var response = await _http.GetAsync(
                    $"https://api.themoviedb.org/3/search/multi" +
                    $"?api_key={_apiKey}" +
                    $"&query={Uri.EscapeDataString(query)}" +
                    $"&language={locale}" +
                    $"&page={page}" +
                    $"&include_adult=false");

                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                if (!json.TryGetProperty("results", out var resultsEl)) return null;

                var items = resultsEl.EnumerateArray()
                    .Where(r => r.TryGetProperty("media_type", out var mt)
                             && (mt.GetString() == "movie" || mt.GetString() == "tv"))
                    .Select(r =>
                    {
                        var mediaType = r.GetProperty("media_type").GetString()!;
                        var isMovie = mediaType == "movie";

                        var title = isMovie
                            ? (r.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "")
                            : (r.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "");

                        var dateStr = isMovie
                            ? (r.TryGetProperty("release_date", out var rd) ? rd.GetString() : null)
                            : (r.TryGetProperty("first_air_date", out var fad) ? fad.GetString() : null);

                        return new TmdbSearchItemDto(
                            TmdbId: r.GetProperty("id").GetInt32(),
                            MediaType: mediaType,
                            Title: title,
                            PosterPath: r.TryGetProperty("poster_path", out var pp) && pp.ValueKind != JsonValueKind.Null
                                ? pp.GetString() : null,
                            Overview: r.TryGetProperty("overview", out var ov) ? ov.GetString() : null,
                            ReleaseDate: dateStr is not null && DateOnly.TryParse(dateStr, out var date) ? date : null,
                            TmdbRating: r.TryGetProperty("vote_average", out var va) ? va.GetSingle() : 0f
                        );
                    })
                    .ToList();

                var totalPages = json.TryGetProperty("total_pages", out var tp) ? tp.GetInt32() : 1;

                return new TmdbSearchResultDto(items, page, totalPages);
            }
        }

        public record MovieCardDto(
            string Title,
            string? PosterPath,
            string? BackdropPath,
            string? Overview
        );

        public record MovieDetailDto(
            int TmdbId,
            string Title,
            string? OriginalTitle,
            string? Overview,
            string? PosterPath,
            string? BackdropPath,
            DateOnly? ReleaseDate,
            int? RuntimeMinutes,
            string[] Genres,
            string? Certification,
            string[] Directors,
            string[] CastTop10,
            float TmdbRating,
            int TmdbVoteCount,
            float TmdbPopularity
        );

        public record SeriesDetailDto(
            int TmdbId,
            string Title,
            string? OriginalTitle,
            string? Overview,
            string? PosterPath,
            string? BackdropPath,
            DateOnly? FirstAirDate,
            int NumberOfSeasons,
            int NumberOfEpisodes,
            string[] Genres,
            string? Certification,
            string[] CreatedBy,
            string[] CastTop10,
            float TmdbRating,
            int TmdbVoteCount,
            float TmdbPopularity,
            TmdbSeasonDto[] Seasons
        );

        // ✅ Liste complète des saisons telles que TMDB les connaît — inclut
        // les saisons que l'utilisateur ne possède pas encore, contrairement à
        // la table locale SeriesSeasons qui n'est peuplée qu'au fil des sync
        // Jellyfin/Plex (donc uniquement les saisons déjà possédées).
        public record TmdbSeasonDto(
            int SeasonNumber,
            int EpisodeCount,
            string? Name,
            string? PosterPath
        );

        public record TmdbSearchItemDto(
            int TmdbId,
            string MediaType, // "movie" | "tv"
            string Title,
            string? PosterPath,
            string? Overview,
            DateOnly? ReleaseDate,
            float TmdbRating
        );

        public record TmdbSearchResultDto(
            List<TmdbSearchItemDto> Results,
            int Page,
            int TotalPages
        );
    }
}
