using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using swipefilm.Auth.swipefilm.Services;
using swipefilm.Data;
using swipefilm.Models;

namespace swipefilm.Auth
{
    public class RecommendationEngine
    {
        private readonly AppDbContext _db;
        private readonly IDbContextFactory<AppDbContext> _dbFactory;
        private readonly IMemoryCache _cache;
        private readonly TmdbService _tmdb;

        public RecommendationEngine(
            AppDbContext db,
            IDbContextFactory<AppDbContext> dbFactory,
            IMemoryCache cache,
            TmdbService tmdb)
        {
            _db = db;
            _dbFactory = dbFactory;
            _cache = cache;
            _tmdb = tmdb;
        }

        /// <summary>
        /// Remplace Title/PosterPath/Overview par la version live TMDB dans la
        /// langue/région de l'utilisateur — ces champs ne sont plus considérés
        /// à jour en base pour l'affichage (seulement un repli si TMDB échoue).
        /// </summary>
        private async Task EnrichSectionWithLiveCardsAsync(
            HomeSection section, string locale, bool isSeries)
        {
            var cards = await Task.WhenAll(section.Movies.Select(async m =>
            {
                var card = isSeries
                    ? await _tmdb.GetSeriesCardAsync(m.TmdbId, locale)
                    : await _tmdb.GetMovieCardAsync(m.TmdbId, locale);
                return (Movie: m, Card: card);
            }));

            foreach (var (movie, card) in cards)
            {
                if (card is null) continue; // repli : garde les valeurs déjà en base
                movie.Title = card.Title;
                movie.PosterPath = card.PosterPath;
                movie.BackdropPath = card.BackdropPath;
                movie.Overview = card.Overview;
            }
        }

        // ─── Profil ───────────────────────────────────────────────────

        public async Task<UserProfile> GetOrCreateProfileAsync(Guid userId, AppDbContext? db = null)
        {
            var cacheKey = $"profile:{userId}";
            if (_cache.TryGetValue(cacheKey, out UserProfile? cached)) return cached!;

            db ??= _db;
            var profile = await db.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile is null)
            {
                profile = new UserProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    UpdatedAt = DateTime.UtcNow
                };
                db.UserProfiles.Add(profile);
                await db.SaveChangesAsync();
            }

            _cache.Set(cacheKey, profile, TimeSpan.FromMinutes(5));
            return profile;
        }

        // ─── Disponibilité ────────────────────────────────────────────

        private async Task<HashSet<int>> GetAvailableMoviesAsync(AppDbContext? db = null)
        {
            const string cacheKey = "available";
            if (_cache.TryGetValue(cacheKey, out HashSet<int>? cached)) return cached!;

            db ??= _db;
            var list = await (db ?? _db).ServerMovie
                .AsNoTracking()
                .Select(x => x.Movie.TmdbId)
                .ToListAsync();

            var result = list.ToHashSet();
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
            return result;
        }

        // ─── Exclusions ───────────────────────────────────────────────

        private async Task<HashSet<int>> GetExcludedMoviesAsync(
            Guid userId, SectionProfile section, AppDbContext? db = null)
        {
            db ??= _db;

            var leftSwipes = await db.Swipes.AsNoTracking()
                .Where(s => s.UserId == userId
                         && s.Direction == SwipeDirection.Left
                         && s.MovieId != null
                         && s.CreatedAt > DateTime.UtcNow.AddDays(-7))
                .Select(s => s.Movie!.TmdbId)
                .ToListAsync();

            var watchedIds = await db.WatchHistory.AsNoTracking()
                .Where(w => w.UserId == userId)
                .Join(db.Movies, w => w.MovieId, m => m.Id, (w, m) => new {
                    w.WatchDurationSec,
                    m.TmdbId,
                    m.RuntimeMinutes
                })
                .Where(x => x.RuntimeMinutes > 0 &&
                    (float)x.WatchDurationSec / (x.RuntimeMinutes!.Value * 60) >= 0.9f)
                .Select(x => x.TmdbId)
                .ToListAsync();

            var rightSwipeIds = new List<int>();
            if (section.Id != "surprise_me")
            {
                rightSwipeIds = await db.Swipes.AsNoTracking()
                    .Where(s => s.UserId == userId
                             && s.Direction == SwipeDirection.Right
                             && s.MovieId != null
                             && s.CreatedAt > DateTime.UtcNow.AddDays(-3))
                    .Select(s => s.Movie!.TmdbId)
                    .ToListAsync();
            }

            return leftSwipes.Concat(watchedIds).Concat(rightSwipeIds).ToHashSet();
        }

        // ─── Candidats ────────────────────────────────────────────────

        private async Task<List<Movie>> BuildCandidatesAsync(
            Guid userId,
            UserProfile userProfile,
            SectionProfile section,
            HashSet<int> excluded,
            HashSet<int> available,
            AvailabilityFilter availability,
            AppDbContext? db = null)
        {
            db ??= _db;

            var query = db.Movies
                .AsNoTracking()
                .Where(m => !excluded.Contains(m.TmdbId)
                         && m.CachedAt != DateTime.MinValue);

            if (section.HiddenGems > 0.5f)
                query = query.Where(m =>
                    m.TmdbPopularity < 20f &&
                    m.TmdbRating >= 7.0f &&
                    m.TmdbVoteCount >= 50);

            if (section.QualityFilter)
                query = query.Where(m => m.TmdbRating >= 6.5f);
            // Dans BuildCandidatesAsync, ajoute ce filtre de base
            query = query.Where(m => m.TmdbVoteCount >= 10);


            if (section.BasedOnMovieId.HasValue)
            {
                var baseMovie = await db.Movies.FindAsync(section.BasedOnMovieId.Value);
                if (baseMovie != null)
                {
                    var genres = baseMovie.Genres;
                    var directors = baseMovie.Directors;
                    query = query.Where(m =>
                        m.Id != baseMovie.Id &&
                        (m.Genres.Any(g => genres.Contains(g)) ||
                         m.Directors.Any(d => directors.Contains(d))));
                }
            }

            if (section.Id == "recent_releases")
            {
                var months = section.ReleasedWithinMonths ?? 6;
                var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-months));
                query = query.Where(m =>
                    m.ReleaseDate.HasValue &&
                    m.ReleaseDate.Value >= cutoff);
            }

            if (availability == AvailabilityFilter.AvailableOnly)
                query = query.Where(m =>
                    db.ServerMovie.Any(sm => sm.MovieId == m.Id));
            else if (availability == AvailabilityFilter.UnavailableOnly)
                query = query.Where(m =>
                    !db.ServerMovie.Any(sm => sm.MovieId == m.Id));
            // ✅ Filtre vote count minimum
            if (section.MinVoteCount > 0)
                query = query.Where(m => m.TmdbVoteCount >= section.MinVoteCount);

            // ✅ Filtre popularité max (HiddenGems)
            if (section.MaxPopularity < float.MaxValue)
                query = query.Where(m => m.TmdbPopularity <= section.MaxPopularity);

            // ✅ Pré-filtre acteurs
            if (section.FilterByTopActors && userProfile.ActorWeights.Any())
            {
                var topActors = userProfile.ActorWeights
                    .Where(a => a.Value > 0.3f &&
                           userProfile.ActorCounts.GetValueOrDefault(a.Key, 0) >= section.MinPersonSignals)
                    .OrderByDescending(a => a.Value)
                    .Take(5)
                    .Select(a => a.Key)
                    .ToList();

                if (topActors.Any())
                    query = query.Where(m => m.CastTop5.Any(a => topActors.Contains(a)));
            }

            // ✅ Pré-filtre réalisateurs
            if (section.FilterByTopDirectors && userProfile.DirectorWeights.Any())
            {
                var topDirs = userProfile.DirectorWeights
                    .Where(d => d.Value > 0.3f &&
                           userProfile.DirectorCounts.GetValueOrDefault(d.Key, 0) >= section.MinPersonSignals)
                    .OrderByDescending(d => d.Value)
                    .Take(3)
                    .Select(d => d.Key)
                    .ToList();

                if (topDirs.Any())
                    query = query.Where(m => m.Directors.Any(d => topDirs.Contains(d)));
            }

            // ✅ Pré-filtre genres ciblés (sections par genre, saisonnier)
            if (section.FilterByTargetGenres && section.TargetGenres?.Any() == true)
            {
                var targetGenres = section.TargetGenres;
                query = query.Where(m => m.Genres.Any(g => targetGenres.Contains(g)));
            }

            return await query
                .OrderBy(_ => EF.Functions.Random())
                .Take(5000)
                .ToListAsync();
        }

        // ─── Page d'accueil ───────────────────────────────────────────

        public async Task<HomePageResult> GetHomePageAsync(
            Guid userId, int page = 1, int pageSize = 5,
            int countPerSection = 20,
            AvailabilityFilter availability = AvailabilityFilter.All,
            HomeContentFilter contentFilter = HomeContentFilter.All,
            HashSet<int>? excludeTmdbIds = null)
        {
            excludeTmdbIds ??= [];
            // ✅ Données partagées — chargées une seule fois avant le parallélisme
            await using var sharedDb = await _dbFactory.CreateDbContextAsync();

            var user = await sharedDb.Users.FindAsync(userId);
            var locale = $"{user?.Locale ?? "fr"}-{user?.Region ?? "FR"}";

            var wantMovies = contentFilter != HomeContentFilter.SeriesOnly;
            var wantSeries = contentFilter != HomeContentFilter.MoviesOnly;

            var userProfile = wantMovies
                ? await GetOrCreateProfileAsync(userId, sharedDb)
                : new UserProfile { UserId = userId };
            var available = wantMovies
                ? await GetAvailableMoviesAsync(sharedDb) : [];
            var excluded = wantMovies
                ? await GetExcludedMoviesAsync(userId, SectionProfile.ForYou, sharedDb) : [];

            // ✅ Exclusions fournies par le client — accumulées côté frontend
            // au fil du scroll (page 1, 2, 3...) et remises à zéro à chaque
            // rechargement de la page. Pas de mémoire côté serveur : un
            // refresh doit pouvoir remontrer les mêmes meilleurs résultats.
            excluded.UnionWith(excludeTmdbIds);

            var basedOnIds = wantMovies
                ? await GetBasedOnMovieIdsAsync(userId, 5, sharedDb) : [];
            var baseMovieTitles = basedOnIds.Any()
                ? await sharedDb.Movies
                    .Where(m => basedOnIds.Contains(m.Id))
                    .ToDictionaryAsync(m => m.Id, m => m.Title)
                : [];

            var seriesProfile = wantSeries
                ? await GetOrCreateSeriesProfileAsync(userId, sharedDb)
                : new UserSeriesProfile { UserId = userId };
            var availableSeries = wantSeries
                ? await GetAvailableSeriesAsync(sharedDb) : [];
            var excludedSeries = wantSeries
                ? await GetExcludedSeriesAsync(userId, SectionProfile.ForYou, sharedDb) : [];

            excludedSeries.UnionWith(excludeTmdbIds);

            var basedOnSeriesIds = wantSeries
                ? await GetBasedOnSeriesIdsAsync(userId, 5, sharedDb) : [];
            var baseSeriesTitles = basedOnSeriesIds.Any()
                ? await sharedDb.Series
                    .Where(s => basedOnSeriesIds.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id, s => s.Title)
                : [];

            // ✅ Liste ordonnée des sections candidates (films + séries) — pas
            // de requête coûteuse ici, seulement les profils déjà chargés. On
            // ne construit que la tranche demandée (pagination = scroll infini).
            var descriptors = BuildSectionDescriptors(
                userId, userProfile, basedOnIds, baseMovieTitles,
                seriesProfile, basedOnSeriesIds, baseSeriesTitles)
                .Where(d => d.IsSeries ? wantSeries : wantMovies)
                .ToList();

            var pageSlice = descriptors
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // ✅ Chaque tâche crée son propre DbContext via _dbFactory — safe en parallèle
            var tasks = pageSlice.Select(d => d.IsSeries
                ? (d.UseCache
                    ? BuildCachedSeriesSectionWrappedAsync(userId, d.Profile, countPerSection, availability, locale)
                    : BuildSeriesSectionFastAsync(d.Profile,
                        countPerSection, availability, seriesProfile, availableSeries, excludedSeries, locale))
                : (d.UseCache
                    ? BuildCachedSectionWrappedAsync(userId, d.Profile, countPerSection, availability, locale)
                    : BuildSectionFastAsync(userId, d.Profile,
                        countPerSection, availability, userProfile, available, excluded, locale)));

            var results = await Task.WhenAll(tasks);

            // ✅ Dédoublonnage inter-sections — les sections sont scorées en
            // parallèle sans se voir entre elles, donc un même titre à fort
            // score peut sortir dans plusieurs sections à la fois. On les
            // parcourt dans l'ordre (déjà trié par poids) et on retire les
            // doublons des sections suivantes plutôt que de la première.
            var seenIds = new HashSet<Guid>();
            var sections = new List<HomeSection>();

            foreach (var result in results)
            {
                if (result is null) continue;

                result.Movies = result.Movies
                    .Where(m => seenIds.Add(m.Id))
                    .ToList();

                if (result.Movies.Count >= 3)
                    sections.Add(result);
            }

            // ✅ Section non-algorithmique — vos demandes passées Available/
            // PartiallyAvailable, triées par date de disponibilité. Seulement
            // en page 1 (scroll infini ne la répète pas à chaque page).
            if (page == 1)
            {
                var recentlyAvailable = await BuildRecentlyAvailableSectionAsync(userId, locale, sharedDb);
                if (recentlyAvailable is not null)
                    sections.Insert(0, recentlyAvailable);
            }

            return new HomePageResult
            {
                Sections = sections,
                Page = page,
                PageSize = pageSize,
                HasMore = page * pageSize < descriptors.Count
            };
        }

        /// <summary>
        /// Films/séries que l'utilisateur a demandés et qui sont devenus
        /// disponibles (ou partiellement, pour les séries) — pas de scoring,
        /// simple lecture chronologique de MediaRequest.AvailableAt.
        /// </summary>
        private async Task<HomeSection?> BuildRecentlyAvailableSectionAsync(
            Guid userId, string locale, AppDbContext db)
        {
            var requests = await db.MediaRequests
                .Include(r => r.Movie)
                .Include(r => r.Series)
                .Where(r => r.UserId == userId
                         && (r.Status == RequestStatus.Available || r.Status == RequestStatus.PartiallyAvailable)
                         && r.AvailableAt != null)
                .OrderByDescending(r => r.AvailableAt)
                .Take(20)
                .ToListAsync();

            if (!requests.Any()) return null;

            var movies = new List<HomeSectionMovie>();

            foreach (var req in requests)
            {
                if (req.Type == MediaRequestType.Movie && req.Movie is not null)
                {
                    var card = await _tmdb.GetMovieCardAsync(req.Movie.TmdbId, locale);
                    movies.Add(new HomeSectionMovie
                    {
                        Id = req.Movie.Id,
                        TmdbId = req.Movie.TmdbId,
                        Title = card?.Title ?? req.Movie.Title,
                        PosterPath = card?.PosterPath ?? req.Movie.PosterPath,
                        BackdropPath = card?.BackdropPath ?? req.Movie.BackdropPath,
                        Overview = card?.Overview ?? req.Movie.Overview,
                        TmdbRating = req.Movie.TmdbRating,
                        RuntimeMinutes = req.Movie.RuntimeMinutes,
                        Genres = req.Movie.Genres,
                        ReleaseDate = req.Movie.ReleaseDate,
                        ContentType = "movie",
                        IsAvailable = true,
                        Score = 0,
                    });
                }
                else if (req.Type == MediaRequestType.Tv && req.Series is not null)
                {
                    var card = await _tmdb.GetSeriesCardAsync(req.Series.TmdbId, locale);
                    movies.Add(new HomeSectionMovie
                    {
                        Id = req.Series.Id,
                        TmdbId = req.Series.TmdbId,
                        Title = card?.Title ?? req.Series.Title,
                        PosterPath = card?.PosterPath ?? req.Series.PosterPath,
                        BackdropPath = card?.BackdropPath ?? req.Series.BackdropPath,
                        Overview = card?.Overview ?? req.Series.Overview,
                        TmdbRating = req.Series.TmdbRating,
                        RuntimeMinutes = null,
                        Genres = req.Series.Genres,
                        ReleaseDate = req.Series.FirstAirDate,
                        ContentType = "series",
                        IsAvailable = true,
                        Score = 0,
                    });
                }
            }

            if (!movies.Any()) return null;

            return new HomeSection
            {
                Id = "recently_available",
                Title = "Vos demandes sont disponibles",
                Movies = movies,
            };
        }

        private async Task<HomeSection?> BuildCachedSectionWrappedAsync(
            Guid userId, SectionProfile profile, int count,
            AvailabilityFilter availability, string locale)
            => await BuildCachedSectionAsync(userId, profile, count, availability, locale);

        private async Task<HomeSection?> BuildCachedSeriesSectionWrappedAsync(
            Guid userId, SectionProfile profile, int count,
            AvailabilityFilter availability, string locale)
            => await BuildCachedSeriesSectionAsync(userId, profile, count, availability, locale);

        // ─── Descripteurs de sections + ordre pondéré ─────────────────

        private sealed class SectionDescriptor
        {
            public required SectionProfile Profile { get; init; }
            public bool UseCache { get; init; }
            public bool IsSeries { get; init; }
        }

        /// <summary>
        /// Construit la liste complète (statique + dynamique, films + séries)
        /// des sections candidates pour la page d'accueil, triée par poids
        /// décroissant. Les sections à poids égal sont mélangées aléatoirement
        /// entre elles, avec un tirage seedé par utilisateur+jour pour que
        /// l'ordre reste stable d'une page à l'autre dans la même journée.
        /// </summary>
        private List<SectionDescriptor> BuildSectionDescriptors(
            Guid userId, UserProfile userProfile,
            List<Guid> basedOnIds, Dictionary<Guid, string> baseMovieTitles,
            UserSeriesProfile seriesProfile,
            List<Guid> basedOnSeriesIds, Dictionary<Guid, string> baseSeriesTitles)
        {
            var descriptors = new List<SectionDescriptor>
            {
                new() { Profile = SectionProfile.ForYou },
                new() { Profile = SectionProfile.HiddenGemsList },
                new() { Profile = SectionProfile.SurpriseMe },
                new() { Profile = SectionProfile.RecentReleases },
                new() { Profile = SectionProfile.DailyDiscovery, UseCache = true },
                new() { Profile = SectionProfile.WeeklyDiscovery, UseCache = true },

                // ─── Séries — mêmes SectionProfile, Id/Title distincts via `with` ──
                new() { Profile = SectionProfile.ForYou with { Id = "for_you_series", Title = "Séries pour vous" }, IsSeries = true },
                new() { Profile = SectionProfile.HiddenGemsList with { Id = "hidden_gems_series", Title = "Séries — trésors cachés" }, IsSeries = true },
                new() { Profile = SectionProfile.SurpriseMe with { Id = "surprise_me_series", Title = "Séries — surprise-moi" }, IsSeries = true },
            };

            if (userProfile.ActorWeights.Any())
                descriptors.Add(new() { Profile = SectionProfile.FavoriteActors });

            if (userProfile.DirectorWeights.Any())
                descriptors.Add(new() { Profile = SectionProfile.FavoriteDirectors });

            foreach (var movieId in basedOnIds)
                descriptors.Add(new()
                {
                    Profile = SectionProfile.BecauseYouLiked(
                        movieId, baseMovieTitles.GetValueOrDefault(movieId))
                });

            var topGenres = userProfile.GenreWeights
                .Where(g => g.Value > 0.3f &&
                       userProfile.GenreCounts.GetValueOrDefault(g.Key, 0) >= 2)
                .OrderByDescending(g => g.Value)
                .Take(6)
                .Select(g => g.Key);

            foreach (var genre in topGenres)
                descriptors.Add(new() { Profile = SectionProfile.ForGenre(genre) });

            var seasonal = SectionProfile.Seasonal();
            if (seasonal is not null)
                descriptors.Add(new() { Profile = seasonal, UseCache = true });

            // ─── Séries — acteurs favoris / créateurs favoris ──────────────
            if (seriesProfile.ActorWeights.Any())
                descriptors.Add(new()
                {
                    Profile = SectionProfile.FavoriteActors with { Id = "favorite_actors_series", Title = "Séries — vos acteurs préférés" },
                    IsSeries = true
                });

            if (seriesProfile.CreatorWeights.Any())
                descriptors.Add(new()
                {
                    Profile = SectionProfile.FavoriteDirectors with { Id = "favorite_creators_series", Title = "Séries — vos créateurs préférés" },
                    IsSeries = true
                });

            // ─── Séries — "parce que vous avez aimé" (Id déjà unique via basedOnSeriesId) ──
            foreach (var seriesId in basedOnSeriesIds)
                descriptors.Add(new()
                {
                    Profile = SectionProfile.BecauseYouLiked(
                        seriesId, baseSeriesTitles.GetValueOrDefault(seriesId)),
                    IsSeries = true
                });

            // ─── Séries — genres du top ─────────────────────────────────
            var topSeriesGenres = seriesProfile.GenreWeights
                .Where(g => g.Value > 0.3f &&
                       seriesProfile.GenreCounts.GetValueOrDefault(g.Key, 0) >= 2)
                .OrderByDescending(g => g.Value)
                .Take(6)
                .Select(g => g.Key);

            foreach (var genre in topSeriesGenres)
                descriptors.Add(new()
                {
                    Profile = SectionProfile.ForGenre(genre) with { Id = $"genre_series_{genre}", Title = $"Séries — {genre}" },
                    IsSeries = true
                });

            var seasonalSeries = SectionProfile.Seasonal();
            if (seasonalSeries is not null)
                descriptors.Add(new()
                {
                    Profile = seasonalSeries with { Id = "seasonal_series", Title = $"{seasonalSeries.Title} (séries)" },
                    UseCache = true,
                    IsSeries = true
                });

            var seed = HashCode.Combine(userId, DateOnly.FromDateTime(DateTime.UtcNow));
            var rng = new Random(seed);

            return descriptors
                .GroupBy(d => d.Profile.Weight)
                .OrderByDescending(g => g.Key)
                .SelectMany(g => g.OrderBy(_ => rng.Next()))
                .ToList();
        }

        // ─── Section fast ─────────────────────────────────────────────

        private async Task<HomeSection?> BuildSectionFastAsync(
            Guid userId, SectionProfile profile, int count,
            AvailabilityFilter availability,
            UserProfile userProfile, HashSet<int> available, HashSet<int> excluded,
            string locale)
        {
            // ✅ Contexte propre à cette tâche — indispensable avec Task.WhenAll
            await using var db = await _dbFactory.CreateDbContextAsync();
            var globalMean = await GetGlobalMeanRatingAsync(db);

            var candidates = await BuildCandidatesAsync(
                userId, userProfile, profile, excluded, available, availability, db);

            var scored = candidates
                .Select(m => new ScoredMovie(m, ComputeScore(m, userProfile, profile, available, globalMean)))
                .ToList();

            var mixed = ApplyMix(scored, count, profile);

            var section = new HomeSection
            {
                Id = profile.Id,
                Title = profile.Title,
                Movies = mixed.Select(m => new HomeSectionMovie
                {
                    Id = m.Movie.Id,
                    TmdbId = m.Movie.TmdbId,
                    Title = m.Movie.Title,
                    PosterPath = m.Movie.PosterPath,
                    Overview = m.Movie.Overview,
                    TmdbRating = m.Movie.TmdbRating,
                    RuntimeMinutes = m.Movie.RuntimeMinutes,
                    Genres = m.Movie.Genres,
                    ReleaseDate = m.Movie.ReleaseDate,
                    ContentType = m.Movie.ContentType,
                    IsAvailable = available.Contains(m.Movie.TmdbId),
                    Score = MathF.Round(m.Score, 3)
                }).ToList()
            };

            await EnrichSectionWithLiveCardsAsync(section, locale, isSeries: false);
            return section;
        }

        // ─── Section avec cache BD ─────────────────────────────────────

        private async Task<HomeSection> BuildCachedSectionAsync(
            Guid userId, SectionProfile profile, int count,
            AvailabilityFilter availability, string locale)
        {
            // ✅ Contexte isolé — safe en parallèle avec les autres sections
            await using var db = await _dbFactory.CreateDbContextAsync();

            var cacheKey = availability == AvailabilityFilter.All
                ? profile.Id
                : $"{profile.Id}_{availability}";

            var cache = await db.DiscoveryCaches
                .FirstOrDefaultAsync(d => d.UserId == userId && d.SectionId == cacheKey);

            var now = DateTime.UtcNow;

            if (cache is not null && cache.ExpiresAt > now && cache.TmdbIds.Any())
            {
                var available = await GetAvailableMoviesAsync(db);

                var movies = await db.Movies
                    .Where(m => cache.TmdbIds.Contains(m.TmdbId))
                    .ToListAsync();

                var ordered = cache.TmdbIds
                    .Select(id => movies.FirstOrDefault(m => m.TmdbId == id))
                    .Where(m => m != null)
                    .Cast<Movie>()
                    .ToList();

                ordered = availability switch
                {
                    AvailabilityFilter.AvailableOnly => ordered.Where(m => available.Contains(m.TmdbId)).ToList(),
                    AvailabilityFilter.UnavailableOnly => ordered.Where(m => !available.Contains(m.TmdbId)).ToList(),
                    _ => ordered
                };

                var cachedSection = new HomeSection
                {
                    Id = profile.Id,
                    Title = profile.Title,
                    Movies = ordered.Select(m => new HomeSectionMovie
                    {
                        Id = m.Id,
                        TmdbId = m.TmdbId,
                        Title = m.Title,
                        PosterPath = m.PosterPath,
                        Overview = m.Overview,
                        TmdbRating = m.TmdbRating,
                        RuntimeMinutes = m.RuntimeMinutes,
                        Genres = m.Genres,
                        ReleaseDate = m.ReleaseDate,
                        ContentType = m.ContentType,
                        IsAvailable = available.Contains(m.TmdbId),
                        Score = 0f
                    }).ToList()
                };

                await EnrichSectionWithLiveCardsAsync(cachedSection, locale, isSeries: false);
                return cachedSection;
            }

            // Cache expiré ou inexistant → génère
            var globalMean = await GetGlobalMeanRatingAsync(db);
            var userProfile = await GetOrCreateProfileAsync(userId, db);
            var excluded = await GetExcludedMoviesAsync(userId, profile, db);
            var avail = await GetAvailableMoviesAsync(db);

            var candidates = await BuildCandidatesAsync(
                userId, userProfile, profile, excluded, avail, availability, db);

            var scored = candidates
                .Select(m => new ScoredMovie(m, ComputeScore(m, userProfile, profile, avail, globalMean)))
                .ToList();

            var mixed = ApplyMix(scored, count, profile);

            var section = new HomeSection
            {
                Id = profile.Id,
                Title = profile.Title,
                Movies = mixed.Select(m => new HomeSectionMovie
                {
                    Id = m.Movie.Id,
                    TmdbId = m.Movie.TmdbId,
                    Title = m.Movie.Title,
                    PosterPath = m.Movie.PosterPath,
                    Overview = m.Movie.Overview,
                    TmdbRating = m.Movie.TmdbRating,
                    RuntimeMinutes = m.Movie.RuntimeMinutes,
                    Genres = m.Movie.Genres,
                    ReleaseDate = m.Movie.ReleaseDate,
                    ContentType = m.Movie.ContentType,
                    IsAvailable = avail.Contains(m.Movie.TmdbId),
                    Score = MathF.Round(m.Score, 3)
                }).ToList()
            };

            // Stocke dans le cache BD
            var expiresAt = profile.RefreshDays.HasValue
                ? now.AddDays(profile.RefreshDays.Value)
                : now.AddDays(1);

            if (cache is null)
            {
                cache = new DiscoveryCache
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    SectionId = cacheKey,
                };
                db.DiscoveryCaches.Add(cache);
            }

            cache.TmdbIds = section.Movies.Select(m => m.TmdbId).ToList();
            cache.GeneratedAt = now;
            cache.ExpiresAt = expiresAt;

            await db.SaveChangesAsync();

            await EnrichSectionWithLiveCardsAsync(section, locale, isSeries: false);
            return section;
        }

        // ─── GetRecommendationsAsync ──────────────────────────────────

        public async Task<(List<ScoredMovie> movies, HashSet<int> available)>
            GetRecommendationsAsync(
                Guid userId,
                SectionProfile profile,
                int count = 20,
                AvailabilityFilter availability = AvailabilityFilter.All,
                HashSet<int>? sessionExcluded = null,
                AppDbContext? db = null)
        {
            var userProfile = await GetOrCreateProfileAsync(userId, db);
            var excluded = await GetExcludedMoviesAsync(userId, profile, db);
            var available = await GetAvailableMoviesAsync(db);
            var globalMean = await GetGlobalMeanRatingAsync(db);


            if (sessionExcluded?.Any() == true)
                excluded.UnionWith(sessionExcluded);

            var candidates = await BuildCandidatesAsync(
                userId, userProfile, profile, excluded, available, availability, db);

            var scored = candidates
                .Select(m => new ScoredMovie(m, ComputeScore(m, userProfile, profile, available, globalMean)))
                .ToList();

            return (ApplyMix(scored, count, profile), available);
        }

        // ─── GetBasedOnMovieIdsAsync ──────────────────────────────────
        // Plusieurs films pivots pour générer plusieurs sections
        // "Parce que vous avez aimé X", au lieu d'un seul.

        private async Task<List<Guid>> GetBasedOnMovieIdsAsync(
            Guid userId, int take, AppDbContext? db = null)
        {
            db ??= _db;

            var lastLiked = await db.Swipes
                .Where(s => s.UserId == userId && s.Direction == SwipeDirection.Right
                         && s.MovieId != null)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => s.MovieId!.Value)
                .Take(take)
                .ToListAsync();

            var result = lastLiked.ToList();
            if (result.Count >= take) return result;

            var topRated = await db.WatchHistory
                .Where(w => w.UserId == userId && w.Movie != null
                         && !result.Contains(w.MovieId))
                .OrderByDescending(w => w.UserRating ?? 0)
                .ThenByDescending(w => w.LastWatchedAt)
                .Select(w => w.MovieId)
                .Take(take - result.Count)
                .ToListAsync();

            result.AddRange(topRated);
            return result;
        }

        // ─── Score ────────────────────────────────────────────────────

        private float ComputeScore(
            Movie movie, UserProfile userProfile,
            SectionProfile section, HashSet<int> available,
            float globalMean)
        {
            float historyScore = ComputeHistoryScore(movie, userProfile);
            float genreScore = ComputeGenreScore(movie, userProfile);
            float actorScore = ComputeActorScore(movie, userProfile);
            float directorScore = ComputeDirectorScore(movie, userProfile);
            float keywordScore = ComputeKeywordScore(movie, userProfile);
            float ratingScore = NormalizeRating(movie, section.UseBayesianRating, globalMean); float popularityScore = ComputePopularityScore(movie);
            float runtimeScore = ComputeRuntimeScore(movie, userProfile);
            float yearScore = ComputeYearScore(movie, userProfile);
            float availScore = available.Contains(movie.TmdbId) ? 1f : 0f;
            float langScore = ComputeLanguageScore(movie, userProfile);

            float hiddenGemScore = 0f;
            if (movie.TmdbPopularity < 20f && movie.TmdbRating >= 7.0f)
                hiddenGemScore = (1f - (movie.TmdbPopularity / 20f)) * ratingScore;

            float score =
                  section.History * historyScore
                + section.Genres * genreScore
                + section.Actors * actorScore
                + section.Directors * directorScore
                + section.Keywords * keywordScore
                + section.Rating * ratingScore
                + section.Popularity * popularityScore
                + section.Runtime * runtimeScore
                + section.Year * yearScore
                + section.Availability * availScore
                + section.HiddenGems * hiddenGemScore
                + section.Language * langScore;

            float totalWeight =
                  section.History + section.Genres + section.Actors
                + section.Directors + section.Keywords + section.Rating
                + section.Popularity + section.Runtime + section.Year
                + section.Availability + section.HiddenGems + section.Language;

            if (totalWeight > 0) score /= totalWeight;

            if (movie.TmdbPopularity < 5f && userProfile.TotalSignals < 10)
                score *= 0.5f;

            return Math.Clamp(score, 0f, 1f);
        }

        private float ComputeHistoryScore(Movie movie, UserProfile profile)
        {
            if (profile.TotalSignals == 0) return 0f;

            // Score basé sur les genres similaires que l'user a bien regardés
            float genreSignal = 0f;
            if (movie.Genres.Any() && profile.GenreWeights.Any())
                genreSignal = movie.Genres
                    .Select(g => profile.GenreWeights.GetValueOrDefault(g, 0f))
                    .Average();

            // Score basé sur les réalisateurs similaires
            float directorSignal = 0f;
            if (movie.Directors.Any() && profile.DirectorWeights.Any())
                directorSignal = movie.Directors
                    .Select(d => profile.DirectorWeights.GetValueOrDefault(d, 0f))
                    .Average();

            // Score basé sur les acteurs similaires
            float actorSignal = 0f;
            if (movie.CastTop5.Any() && profile.ActorWeights.Any())
                actorSignal = movie.CastTop5
                    .Select(a => profile.ActorWeights.GetValueOrDefault(a, 0f))
                    .Average();

            // Bonus comportemental global de l'user (complète le signal contenu)
            float completionBonus = Math.Clamp(profile.AvgCompletionRate / 100f, 0f, 1f);

            float raw = (genreSignal * 0.5f)
                      + (directorSignal * 0.3f)
                      + (actorSignal * 0.1f)
                      + (completionBonus * 0.1f);

            // Convertit [-1, 1] → [0, 1]
            return Math.Clamp((raw + 1f) / 2f, 0f, 1f);
        }

        private float ComputeLanguageScore(Movie movie, UserProfile profile)
        {
            if (string.IsNullOrEmpty(movie.OriginalLanguage)) return 0.5f;
            if (!profile.OriginalLanguageWeights.Any()) return 0.5f;
            return profile.OriginalLanguageWeights.TryGetValue(
                movie.OriginalLanguage, out var w) ? (w + 1f) / 2f : 0.2f;
        }

        private float ComputeGenreScore(Movie movie, UserProfile profile)
        {
            if (!profile.GenreWeights.Any() || !movie.Genres.Any()) return 0.5f;
            var raw = movie.Genres
                .Select(g => profile.GenreWeights.GetValueOrDefault(g, 0f)).Average();
            return (raw + 1f) / 2f;
        }

        private float ComputeActorScore(Movie movie, UserProfile profile)
        {
            if (!profile.ActorWeights.Any() || !movie.CastTop5.Any()) return 0.5f;
            var raw = movie.CastTop5
                .Select(a => profile.ActorWeights.GetValueOrDefault(a, 0f)).Average();
            return (raw + 1f) / 2f;
        }

        private float ComputeDirectorScore(Movie movie, UserProfile profile)
        {
            if (!profile.DirectorWeights.Any() || !movie.Directors.Any()) return 0f;
            var raw = movie.Directors
                .Select(d => profile.DirectorWeights.GetValueOrDefault(d, 0f)).Average();
            return (raw + 1f) / 2f;
        }

        private float ComputeKeywordScore(Movie movie, UserProfile profile)
        {
            if (!profile.KeywordWeights.Any() || !movie.Keywords.Any()) return 0.5f;
            var raw = movie.Keywords
                .Select(k => profile.KeywordWeights.GetValueOrDefault(k, 0f))
                .DefaultIfEmpty(0f).Average();
            return (raw + 1f) / 2f;
        }

        private float ComputeRuntimeScore(Movie movie, UserProfile profile)
        {
            if (!movie.RuntimeMinutes.HasValue || profile.PreferredRuntimeMax <= 0) return 0.5f;
            var diff = Math.Abs(movie.RuntimeMinutes.Value - profile.PreferredRuntimeMax);
            return Math.Max(0f, 1f - (diff / 60f));
        }

        private float ComputeYearScore(Movie movie, UserProfile profile)
        {
            if (!movie.ReleaseDate.HasValue || profile.PreferredMinYear <= 0) return 0.5f;

            var year = movie.ReleaseDate.Value.Year;
            var target = profile.PreferredMinYear;
            var diff = Math.Abs(year - target);

            // Pénalité symétrique — 20 ans d'écart = score 0
            return Math.Max(0f, 1f - (diff / 20f));
        }

        private float ComputePopularityScore(Movie movie) =>
            Math.Min(movie.TmdbPopularity / 100f, 1f);

        private float NormalizeRating(Movie movie, bool useBayesian, float globalMean)
        {
            if (!useBayesian || movie.TmdbVoteCount == 0)
                return Math.Clamp(movie.TmdbRating / 10f, 0f, 1f);

            const int m = 50; // votes minimum de référence
            var v = movie.TmdbVoteCount;
            var R = movie.TmdbRating;
            var bayesian = (v / (float)(v + m)) * R + (m / (float)(v + m)) * globalMean;
            return Math.Clamp(bayesian / 10f, 0f, 1f);
        }
        private async Task<float> GetGlobalMeanRatingAsync(AppDbContext? db = null)
        {
            const string key = "global_mean_rating";
            if (_cache.TryGetValue(key, out float cached)) return cached;

            db ??= _db;
            var mean = await db.Movies
                .AsNoTracking()
                .Where(m => m.TmdbVoteCount > 0)
                .AverageAsync(m => (double)m.TmdbRating);

            var result = (float)mean;
            _cache.Set(key, result, TimeSpan.FromHours(1));
            return result;
        }

        // ─── Mix ──────────────────────────────────────────────────────

        private List<ScoredMovie> ApplyMix(
            List<ScoredMovie> scored, int count, SectionProfile section)
        {
            var sorted = scored.OrderByDescending(s => s.Score).ToList();

            int topCount = Math.Max(0, Math.Min((int)(count * (1f - section.Randomness)), sorted.Count));
            int randomCount = Math.Max(0, Math.Min(count - topCount, sorted.Count - topCount));

            var top = sorted.Take(topCount).ToList();
            var rest = sorted.Skip(topCount)
                .OrderBy(_ => Random.Shared.Next())
                .Take(randomCount).ToList();

            var result = new List<ScoredMovie>();
            int interval = topCount > 0 && randomCount > 0
                ? Math.Max(1, topCount / randomCount) : int.MaxValue;
            int restIdx = 0;

            for (int i = 0; i < top.Count; i++)
            {
                result.Add(top[i]);
                if (restIdx < rest.Count && i > 0 && i % interval == 0)
                    result.Add(rest[restIdx++]);
            }

            while (restIdx < rest.Count) result.Add(rest[restIdx++]);
            return result.Take(count).ToList();
        }

        // ════════════════════════════════════════════════════════════
        // ─── Séries — chemin parallèle, mêmes SectionProfile ─────────
        // Series a une forme différente de Movie (CreatedBy au lieu de
        // Directors, pas de RuntimeMinutes unique pertinent) donc un
        // chemin de scoring dédié plutôt qu'une interface commune forcée,
        // cf. le plan. Les SectionProfile (poids, filtres) sont réutilisés
        // tels quels.
        // ════════════════════════════════════════════════════════════

        private async Task<HashSet<int>> GetAvailableSeriesAsync(AppDbContext? db = null)
        {
            const string cacheKey = "available_series";
            if (_cache.TryGetValue(cacheKey, out HashSet<int>? cached)) return cached!;

            db ??= _db;
            var list = await db.ServerSeries
                .AsNoTracking()
                .Select(x => x.Series.TmdbId)
                .ToListAsync();

            var result = list.ToHashSet();
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
            return result;
        }

        private async Task<HashSet<int>> GetExcludedSeriesAsync(
            Guid userId, SectionProfile section, AppDbContext? db = null)
        {
            db ??= _db;

            var leftSwipes = await db.Swipes.AsNoTracking()
                .Where(s => s.UserId == userId
                         && s.Direction == SwipeDirection.Left
                         && s.SeriesId != null
                         && s.CreatedAt > DateTime.UtcNow.AddDays(-7))
                .Select(s => s.Series!.TmdbId)
                .ToListAsync();

            // Séries dont l'utilisateur a fini toutes les saisons possédées
            var watchedIds = await db.SeriesWatchHistory.AsNoTracking()
                .Where(w => w.UserId == userId)
                .Join(db.SeriesSeasons, w => w.SeriesSeasonId, s => s.Id,
                    (w, s) => new { w.WatchedEpisodeCount, s.EpisodeCount, s.SeriesId })
                .Where(x => x.EpisodeCount > 0
                    && (float)x.WatchedEpisodeCount / x.EpisodeCount >= 0.9f)
                .Join(db.Series, x => x.SeriesId, sr => sr.Id, (x, sr) => sr.TmdbId)
                .Distinct()
                .ToListAsync();

            var rightSwipeIds = new List<int>();
            if (section.Id != "surprise_me")
            {
                rightSwipeIds = await db.Swipes.AsNoTracking()
                    .Where(s => s.UserId == userId
                             && s.Direction == SwipeDirection.Right
                             && s.SeriesId != null
                             && s.CreatedAt > DateTime.UtcNow.AddDays(-3))
                    .Select(s => s.Series!.TmdbId)
                    .ToListAsync();
            }

            return leftSwipes.Concat(watchedIds).Concat(rightSwipeIds).ToHashSet();
        }

        /// <summary>Plusieurs séries pivots pour "Parce que vous avez aimé" — mirroir de GetBasedOnMovieIdsAsync.</summary>
        private async Task<List<Guid>> GetBasedOnSeriesIdsAsync(
            Guid userId, int take, AppDbContext? db = null)
        {
            db ??= _db;

            var lastLiked = await db.Swipes
                .Where(s => s.UserId == userId && s.Direction == SwipeDirection.Right
                         && s.SeriesId != null)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => s.SeriesId!.Value)
                .Take(take)
                .ToListAsync();

            var result = lastLiked.ToList();
            if (result.Count >= take) return result;

            var mostEngaged = await db.SeriesWatchHistory
                .Where(w => w.UserId == userId && !result.Contains(w.SeriesSeason.SeriesId))
                .OrderByDescending(w => w.UserRating ?? 0)
                .ThenByDescending(w => w.LastWatchedAt)
                .Select(w => w.SeriesSeason.SeriesId)
                .Distinct()
                .Take(take - result.Count)
                .ToListAsync();

            result.AddRange(mostEngaged);
            return result;
        }

        private async Task<List<Series>> BuildSeriesCandidatesAsync(
            UserSeriesProfile userProfile,
            SectionProfile section,
            HashSet<int> excluded,
            AvailabilityFilter availability,
            AppDbContext? db = null)
        {
            db ??= _db;

            var query = db.Series
                .AsNoTracking()
                .Where(s => !excluded.Contains(s.TmdbId) && s.CachedAt != DateTime.MinValue);

            if (section.QualityFilter)
                query = query.Where(s => s.TmdbRating >= 6.5f);
            query = query.Where(s => s.TmdbVoteCount >= 10);

            if (section.BasedOnMovieId.HasValue)
            {
                var baseSeries = await db.Series.FindAsync(section.BasedOnMovieId.Value);
                if (baseSeries != null)
                {
                    var genres = baseSeries.Genres;
                    var creators = baseSeries.CreatedBy;
                    query = query.Where(s =>
                        s.Id != baseSeries.Id &&
                        (s.Genres.Any(g => genres.Contains(g)) ||
                         s.CreatedBy.Any(c => creators.Contains(c))));
                }
            }

            if (section.ReleasedWithinMonths.HasValue)
            {
                var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-section.ReleasedWithinMonths.Value));
                query = query.Where(s => s.FirstAirDate.HasValue && s.FirstAirDate.Value >= cutoff);
            }

            if (availability == AvailabilityFilter.AvailableOnly)
                query = query.Where(s =>
                    db.ServerSeries.Any(ss => ss.SeriesId == s.Id));
            else if (availability == AvailabilityFilter.UnavailableOnly)
                query = query.Where(s =>
                    !db.ServerSeries.Any(ss => ss.SeriesId == s.Id));

            if (section.MinVoteCount > 0)
                query = query.Where(s => s.TmdbVoteCount >= section.MinVoteCount);

            if (section.MaxPopularity < float.MaxValue)
                query = query.Where(s => s.TmdbPopularity <= section.MaxPopularity);

            if (section.FilterByTopActors && userProfile.ActorWeights.Any())
            {
                var topActors = userProfile.ActorWeights
                    .Where(a => a.Value > 0.3f &&
                           userProfile.ActorCounts.GetValueOrDefault(a.Key, 0) >= section.MinPersonSignals)
                    .OrderByDescending(a => a.Value)
                    .Take(5)
                    .Select(a => a.Key)
                    .ToList();

                if (topActors.Any())
                    query = query.Where(s => s.CastTop5.Any(a => topActors.Contains(a)));
            }

            // FilterByTopDirectors → pour les séries, "top créateurs" (CreatedBy)
            if (section.FilterByTopDirectors && userProfile.CreatorWeights.Any())
            {
                var topCreators = userProfile.CreatorWeights
                    .Where(c => c.Value > 0.3f &&
                           userProfile.CreatorCounts.GetValueOrDefault(c.Key, 0) >= section.MinPersonSignals)
                    .OrderByDescending(c => c.Value)
                    .Take(3)
                    .Select(c => c.Key)
                    .ToList();

                if (topCreators.Any())
                    query = query.Where(s => s.CreatedBy.Any(c => topCreators.Contains(c)));
            }

            if (section.FilterByTargetGenres && section.TargetGenres?.Any() == true)
            {
                var targetGenres = section.TargetGenres;
                query = query.Where(s => s.Genres.Any(g => targetGenres.Contains(g)));
            }

            return await query
                .OrderBy(_ => EF.Functions.Random())
                .Take(5000)
                .ToListAsync();
        }

        private float ComputeSeriesScore(
            Series series, UserSeriesProfile userProfile,
            SectionProfile section, HashSet<int> available, float globalMean)
        {
            float historyScore = ComputeSeriesHistoryScore(series, userProfile);
            float genreScore = ComputeSeriesGenreScore(series, userProfile);
            float actorScore = ComputeSeriesActorScore(series, userProfile);
            float creatorScore = ComputeSeriesCreatorScore(series, userProfile);
            float keywordScore = ComputeSeriesKeywordScore(series, userProfile);
            float ratingScore = NormalizeSeriesRating(series, section.UseBayesianRating, globalMean);
            float popularityScore = Math.Min(series.TmdbPopularity / 100f, 1f);
            float availScore = available.Contains(series.TmdbId) ? 1f : 0f;
            float langScore = ComputeSeriesLanguageScore(series, userProfile);

            float hiddenGemScore = 0f;
            if (series.TmdbPopularity < 20f && series.TmdbRating >= 7.0f)
                hiddenGemScore = (1f - (series.TmdbPopularity / 20f)) * ratingScore;

            float score =
                  section.History * historyScore
                + section.Genres * genreScore
                + section.Actors * actorScore
                + section.Directors * creatorScore
                + section.Keywords * keywordScore
                + section.Rating * ratingScore
                + section.Popularity * popularityScore
                + section.Availability * availScore
                + section.HiddenGems * hiddenGemScore
                + section.Language * langScore;

            float totalWeight =
                  section.History + section.Genres + section.Actors
                + section.Directors + section.Keywords + section.Rating
                + section.Popularity + section.Availability
                + section.HiddenGems + section.Language;

            if (totalWeight > 0) score /= totalWeight;

            if (series.TmdbPopularity < 5f && userProfile.TotalSeriesSignals < 10)
                score *= 0.5f;

            return Math.Clamp(score, 0f, 1f);
        }

        private float ComputeSeriesHistoryScore(Series series, UserSeriesProfile profile)
        {
            if (profile.TotalSeriesSignals == 0) return 0f;

            float genreSignal = series.Genres.Any() && profile.GenreWeights.Any()
                ? series.Genres.Select(g => profile.GenreWeights.GetValueOrDefault(g, 0f)).Average()
                : 0f;

            float creatorSignal = series.CreatedBy.Any() && profile.CreatorWeights.Any()
                ? series.CreatedBy.Select(c => profile.CreatorWeights.GetValueOrDefault(c, 0f)).Average()
                : 0f;

            float actorSignal = series.CastTop5.Any() && profile.ActorWeights.Any()
                ? series.CastTop5.Select(a => profile.ActorWeights.GetValueOrDefault(a, 0f)).Average()
                : 0f;

            float completionBonus = Math.Clamp(profile.AvgSeasonCompletionRate / 100f, 0f, 1f);

            float raw = (genreSignal * 0.5f) + (creatorSignal * 0.3f)
                      + (actorSignal * 0.1f) + (completionBonus * 0.1f);

            return Math.Clamp((raw + 1f) / 2f, 0f, 1f);
        }

        private float ComputeSeriesGenreScore(Series series, UserSeriesProfile profile)
        {
            if (!profile.GenreWeights.Any() || !series.Genres.Any()) return 0.5f;
            var raw = series.Genres.Select(g => profile.GenreWeights.GetValueOrDefault(g, 0f)).Average();
            return (raw + 1f) / 2f;
        }

        private float ComputeSeriesActorScore(Series series, UserSeriesProfile profile)
        {
            if (!profile.ActorWeights.Any() || !series.CastTop5.Any()) return 0.5f;
            var raw = series.CastTop5.Select(a => profile.ActorWeights.GetValueOrDefault(a, 0f)).Average();
            return (raw + 1f) / 2f;
        }

        private float ComputeSeriesCreatorScore(Series series, UserSeriesProfile profile)
        {
            if (!profile.CreatorWeights.Any() || !series.CreatedBy.Any()) return 0f;
            var raw = series.CreatedBy.Select(c => profile.CreatorWeights.GetValueOrDefault(c, 0f)).Average();
            return (raw + 1f) / 2f;
        }

        private float ComputeSeriesKeywordScore(Series series, UserSeriesProfile profile)
        {
            if (!profile.KeywordWeights.Any() || !series.Keywords.Any()) return 0.5f;
            var raw = series.Keywords.Select(k => profile.KeywordWeights.GetValueOrDefault(k, 0f))
                .DefaultIfEmpty(0f).Average();
            return (raw + 1f) / 2f;
        }

        private float ComputeSeriesLanguageScore(Series series, UserSeriesProfile profile)
        {
            if (string.IsNullOrEmpty(series.OriginalLanguage)) return 0.5f;
            if (!profile.OriginalLanguageWeights.Any()) return 0.5f;
            return profile.OriginalLanguageWeights.TryGetValue(
                series.OriginalLanguage, out var w) ? (w + 1f) / 2f : 0.2f;
        }

        private float NormalizeSeriesRating(Series series, bool useBayesian, float globalMean)
        {
            if (!useBayesian || series.TmdbVoteCount == 0)
                return Math.Clamp(series.TmdbRating / 10f, 0f, 1f);

            const int m = 50;
            var v = series.TmdbVoteCount;
            var R = series.TmdbRating;
            var bayesian = (v / (float)(v + m)) * R + (m / (float)(v + m)) * globalMean;
            return Math.Clamp(bayesian / 10f, 0f, 1f);
        }

        private async Task<float> GetGlobalMeanSeriesRatingAsync(AppDbContext? db = null)
        {
            const string key = "global_mean_series_rating";
            if (_cache.TryGetValue(key, out float cached)) return cached;

            db ??= _db;
            var hasAny = await db.Series.AsNoTracking().AnyAsync(s => s.TmdbVoteCount > 0);
            var mean = hasAny
                ? (float)await db.Series.AsNoTracking()
                    .Where(s => s.TmdbVoteCount > 0)
                    .AverageAsync(s => (double)s.TmdbRating)
                : 5f;

            _cache.Set(key, mean, TimeSpan.FromHours(1));
            return mean;
        }

        private List<ScoredSeries> ApplyMixSeries(
            List<ScoredSeries> scored, int count, SectionProfile section)
        {
            var sorted = scored.OrderByDescending(s => s.Score).ToList();

            int topCount = Math.Max(0, Math.Min((int)(count * (1f - section.Randomness)), sorted.Count));
            int randomCount = Math.Max(0, Math.Min(count - topCount, sorted.Count - topCount));

            var top = sorted.Take(topCount).ToList();
            var rest = sorted.Skip(topCount)
                .OrderBy(_ => Random.Shared.Next())
                .Take(randomCount).ToList();

            var result = new List<ScoredSeries>();
            int interval = topCount > 0 && randomCount > 0
                ? Math.Max(1, topCount / randomCount) : int.MaxValue;
            int restIdx = 0;

            for (int i = 0; i < top.Count; i++)
            {
                result.Add(top[i]);
                if (restIdx < rest.Count && i > 0 && i % interval == 0)
                    result.Add(rest[restIdx++]);
            }

            while (restIdx < rest.Count) result.Add(rest[restIdx++]);
            return result.Take(count).ToList();
        }

        private static HomeSectionMovie ToHomeSectionMovie(Series s, HashSet<int> available, float score) => new()
        {
            Id = s.Id,
            TmdbId = s.TmdbId,
            Title = s.Title,
            PosterPath = s.PosterPath,
            Overview = s.Overview,
            TmdbRating = s.TmdbRating,
            RuntimeMinutes = null,
            Genres = s.Genres,
            ReleaseDate = s.FirstAirDate,
            ContentType = "series",
            IsAvailable = available.Contains(s.TmdbId),
            Score = MathF.Round(score, 3)
        };

        private async Task<HomeSection?> BuildSeriesSectionFastAsync(
            SectionProfile profile, int count,
            AvailabilityFilter availability,
            UserSeriesProfile userProfile, HashSet<int> available, HashSet<int> excluded,
            string locale)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var globalMean = await GetGlobalMeanSeriesRatingAsync(db);

            var candidates = await BuildSeriesCandidatesAsync(
                userProfile, profile, excluded, availability, db);

            var scored = candidates
                .Select(s => new ScoredSeries(s, ComputeSeriesScore(s, userProfile, profile, available, globalMean)))
                .ToList();

            var mixed = ApplyMixSeries(scored, count, profile);

            var section = new HomeSection
            {
                Id = profile.Id,
                Title = profile.Title,
                Movies = mixed.Select(m => ToHomeSectionMovie(m.Series, available, m.Score)).ToList()
            };

            await EnrichSectionWithLiveCardsAsync(section, locale, isSeries: true);
            return section;
        }

        private async Task<HomeSection> BuildCachedSeriesSectionAsync(
            Guid userId, SectionProfile profile, int count,
            AvailabilityFilter availability, string locale)
        {
            await using var db = await _dbFactory.CreateDbContextAsync();

            var cacheKey = availability == AvailabilityFilter.All
                ? profile.Id
                : $"{profile.Id}_{availability}";

            var cache = await db.DiscoveryCaches
                .FirstOrDefaultAsync(d => d.UserId == userId && d.SectionId == cacheKey);

            var now = DateTime.UtcNow;

            if (cache is not null && cache.ExpiresAt > now && cache.TmdbIds.Any())
            {
                var available = await GetAvailableSeriesAsync(db);
                var series = await db.Series.Where(s => cache.TmdbIds.Contains(s.TmdbId)).ToListAsync();

                var ordered = cache.TmdbIds
                    .Select(id => series.FirstOrDefault(s => s.TmdbId == id))
                    .Where(s => s != null)
                    .Cast<Series>()
                    .ToList();

                ordered = availability switch
                {
                    AvailabilityFilter.AvailableOnly => ordered.Where(s => available.Contains(s.TmdbId)).ToList(),
                    AvailabilityFilter.UnavailableOnly => ordered.Where(s => !available.Contains(s.TmdbId)).ToList(),
                    _ => ordered
                };

                var cachedSection = new HomeSection
                {
                    Id = profile.Id,
                    Title = profile.Title,
                    Movies = ordered.Select(s => ToHomeSectionMovie(s, available, 0f)).ToList()
                };

                await EnrichSectionWithLiveCardsAsync(cachedSection, locale, isSeries: true);
                return cachedSection;
            }

            var globalMean = await GetGlobalMeanSeriesRatingAsync(db);
            var userProfile = await GetOrCreateSeriesProfileAsync(userId, db);
            var excluded = await GetExcludedSeriesAsync(userId, profile, db);
            var avail = await GetAvailableSeriesAsync(db);

            var candidates = await BuildSeriesCandidatesAsync(
                userProfile, profile, excluded, availability, db);

            var scored = candidates
                .Select(s => new ScoredSeries(s, ComputeSeriesScore(s, userProfile, profile, avail, globalMean)))
                .ToList();

            var mixed = ApplyMixSeries(scored, count, profile);

            var section = new HomeSection
            {
                Id = profile.Id,
                Title = profile.Title,
                Movies = mixed.Select(m => ToHomeSectionMovie(m.Series, avail, m.Score)).ToList()
            };

            var expiresAt = profile.RefreshDays.HasValue
                ? now.AddDays(profile.RefreshDays.Value)
                : now.AddDays(1);

            if (cache is null)
            {
                cache = new DiscoveryCache { Id = Guid.NewGuid(), UserId = userId, SectionId = cacheKey };
                db.DiscoveryCaches.Add(cache);
            }

            cache.TmdbIds = section.Movies.Select(m => m.TmdbId).ToList();
            cache.GeneratedAt = now;
            cache.ExpiresAt = expiresAt;

            await db.SaveChangesAsync();

            await EnrichSectionWithLiveCardsAsync(section, locale, isSeries: true);
            return section;
        }

        // ─── Mise à jour profil (swipe) ───────────────────────────────

        public async Task UpdateProfileAsync(
            Guid userId, Guid movieId, SwipeDirection direction, int durationMs,
            AppDbContext? db = null)
        {
            db ??= _db;

            var profile = await GetOrCreateProfileAsync(userId, db);
            var movie = await db.Movies.FindAsync(movieId);
            if (movie is null) return;

            float signal = direction == SwipeDirection.Right
                ? durationMs < 1500 ? 0.4f : 0.3f
                : durationMs < 1500 ? -0.28f : -0.12f;

            UpdateWeightsIncremental(movie.Genres, signal, profile.GenreWeights, profile.GenreCounts);
            UpdateWeightsIncremental(movie.Directors, signal, profile.DirectorWeights, profile.DirectorCounts);
            UpdateWeightsIncremental(movie.CastTop5, signal, profile.ActorWeights, profile.ActorCounts);
            UpdateWeightsIncremental(movie.Keywords, signal, profile.KeywordWeights, profile.KeywordCounts);

            profile.TotalSignals++;
            profile.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();

            // ✅ Invalide le cache pour que le prochain appel recharge le profil à jour
            _cache.Remove($"profile:{userId}");
        }

        /// <summary>
        /// Met à jour le profil en tâche de fond, avec un DbContext indépendant
        /// de celui de la requête HTTP en cours (qui sera disposé dès la réponse
        /// envoyée). À appeler depuis un contrôleur qui veut répondre sans
        /// attendre la fin de la mise à jour du profil.
        /// </summary>
        public void FireAndForgetProfileUpdate(
            Guid userId, Guid movieId, SwipeDirection direction, int durationMs)
        {
            _ = Task.Run(async () =>
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
                await UpdateProfileAsync(userId, movieId, direction, durationMs, db);
            });
        }

        // ─── Mise à jour profil complète ──────────────────────────────

        public async Task UpdateProfileFromHistoryAsync(Guid userId)
        {
            var profile = await GetOrCreateProfileAsync(userId);

            profile.GenreWeights = new Dictionary<string, float>();
            profile.DirectorWeights = new Dictionary<string, float>();
            profile.ActorWeights = new Dictionary<string, float>();
            profile.KeywordWeights = new Dictionary<string, float>();
            profile.TotalSignals = 0;
            var decadeScores = new Dictionary<string, float>();
            profile.GenreCounts = new Dictionary<string, int>();
            profile.DirectorCounts = new Dictionary<string, int>();
            profile.ActorCounts = new Dictionary<string, int>();
            profile.KeywordCounts = new Dictionary<string, int>();
            profile.LanguageCounts = new Dictionary<string, int>();

            var history = await _db.WatchHistory
                .Include(w => w.Movie)
                .Where(w => w.UserId == userId && w.Movie != null)
                .ToListAsync();

            var deduped = history
                .GroupBy(w => w.MovieId)
                .Select(g => g.OrderByDescending(w => w.WatchDurationSec).First())
                .ToList();

            foreach (var w in deduped)
            {
                if (w.Movie is null) continue;

                var completionPct = w.Movie.RuntimeMinutes > 0
                    ? (float)w.WatchDurationSec / (w.Movie.RuntimeMinutes!.Value * 60) * 100f
                    : 50f;

                if (completionPct < 5f && !w.IsFavorite) continue;

                float behaviorSignal = completionPct switch
                {
                    >= 90 => 1.0f,
                    >= 70 => 0.75f,
                    >= 40 => 0.3f,
                    >= 20 => 0.1f,
                    _ => 0.0f
                };

                if (w.UserRating.HasValue)
                    behaviorSignal = (behaviorSignal + (w.UserRating.Value - 5f) / 5f) / 2f;
                if (w.IsFavorite) behaviorSignal = MathF.Min(behaviorSignal + 0.2f, 1f);
                if (w.ViewCount > 1) behaviorSignal = MathF.Min(behaviorSignal + 0.15f * (w.ViewCount - 1), 1f);

                float contentSignal = behaviorSignal;

                UpdateWeightsIncremental(w.Movie.Genres, contentSignal, profile.GenreWeights, profile.GenreCounts);
                UpdateWeightsIncremental(w.Movie.Directors, contentSignal, profile.DirectorWeights, profile.DirectorCounts);
                UpdateWeightsIncremental(w.Movie.CastTop5, contentSignal, profile.ActorWeights, profile.ActorCounts);
                UpdateWeightsIncremental(w.Movie.Keywords, contentSignal, profile.KeywordWeights, profile.KeywordCounts);

                if (!string.IsNullOrEmpty(w.Movie.OriginalLanguage))
                    UpdateWeightsIncremental(
                        [w.Movie.OriginalLanguage], contentSignal, profile.OriginalLanguageWeights, profile.LanguageCounts);

                if (w.Movie.ReleaseDate.HasValue)
                {
                    var decade = (w.Movie.ReleaseDate.Value.Year / 10 * 10).ToString();
                    var daysSince = (DateTime.UtcNow - w.LastWatchedAt).TotalDays;
                    var recency = (float)Math.Max(0.3, 1.0 - (daysSince / 730.0));
                    decadeScores[decade] = decadeScores.GetValueOrDefault(decade, 0f)
                        + (behaviorSignal * recency);
                }

                profile.TotalSignals++;
            }

            var validHistory = deduped.Where(w => w.Movie?.RuntimeMinutes > 0).ToList();
            if (validHistory.Any())
            {
                profile.AvgCompletionRate = validHistory
                    .Select(w => Math.Min(
                        (float)w.WatchDurationSec / (w.Movie!.RuntimeMinutes!.Value * 60) * 100f, 100f))
                    .Average();

                var rated = validHistory.Where(w => w.UserRating.HasValue).ToList();
                profile.AvgUserRating = rated.Any()
                    ? rated.Average(w => (float)w.UserRating!.Value) : 0f;

                profile.FavoriteCount = validHistory.Count(w => w.IsFavorite);
                profile.RepeatViewRate = (float)validHistory.Count(w => w.ViewCount > 1)
                    / validHistory.Count;
            }

            var swipes = await _db.Swipes
                .Include(s => s.Movie)
                .Where(s => s.UserId == userId && s.Movie != null)
                .ToListAsync();

            foreach (var swipe in swipes)
            {
                if (swipe.Movie is null) continue;

                float signal = swipe.Direction == SwipeDirection.Right
                    ? swipe.DurationMs < 1500 ? 0.4f : 0.3f
                    : swipe.DurationMs < 1500 ? -0.2f : -0.12f;

                UpdateWeightsIncremental(swipe.Movie.Genres, signal, profile.GenreWeights, profile.GenreCounts);
                UpdateWeightsIncremental(swipe.Movie.Directors, signal, profile.DirectorWeights, profile.DirectorCounts);
                UpdateWeightsIncremental(swipe.Movie.CastTop5, signal, profile.ActorWeights, profile.ActorCounts);
                UpdateWeightsIncremental(swipe.Movie.Keywords, signal, profile.KeywordWeights, profile.KeywordCounts);

                float langSignal = signal * 0.1f;
                if (!string.IsNullOrEmpty(swipe.Movie.OriginalLanguage))
                    UpdateWeightsIncremental(
                        [swipe.Movie.OriginalLanguage], langSignal, profile.OriginalLanguageWeights, profile.LanguageCounts);

                if (swipe.Movie.ReleaseDate.HasValue)
                {
                    var decade = (swipe.Movie.ReleaseDate.Value.Year / 10 * 10).ToString();
                    var daysSince = (DateTime.UtcNow - swipe.CreatedAt).TotalDays;
                    var recency = (float)Math.Max(0.3, 1.0 - (daysSince / 730.0));
                    decadeScores[decade] = decadeScores.GetValueOrDefault(decade, 0f)
                        + (signal * recency * 0.2f);
                }

                profile.TotalSignals++;
            }

            profile.GenreWeights = Normalize(profile.GenreWeights, profile.GenreCounts);
            profile.DirectorWeights = Normalize(profile.DirectorWeights, profile.DirectorCounts);
            profile.ActorWeights = Normalize(profile.ActorWeights, profile.ActorCounts);
            profile.KeywordWeights = Normalize(profile.KeywordWeights, profile.KeywordCounts);
            profile.OriginalLanguageWeights = Normalize(profile.OriginalLanguageWeights, profile.LanguageCounts);
            profile.PreferredDecadeWeights = Normalize(decadeScores); // pas de compteurs pour les décennies
            profile.UpdatedAt = DateTime.UtcNow;

            var likedMovies = new List<Movie>();
            foreach (var w in deduped)
            {
                if (w.Movie?.RuntimeMinutes is null) continue;
                var pct = (float)w.WatchDurationSec / (w.Movie.RuntimeMinutes.Value * 60) * 100f;
                if (pct >= 70f) likedMovies.Add(w.Movie);
            }
            foreach (var swipe in swipes.Where(s => s.Direction == SwipeDirection.Right))
                if (swipe.Movie is not null) likedMovies.Add(swipe.Movie);

            if (likedMovies.Any())
            {
                var years = likedMovies
                    .Where(m => m.ReleaseDate.HasValue)
                    .Select(m => (float)m.ReleaseDate!.Value.Year).ToList();
                if (years.Any())
                    profile.PreferredMinYear = years.Average();

                var runtimes = likedMovies
                    .Where(m => m.RuntimeMinutes.HasValue && m.RuntimeMinutes > 0)
                    .Select(m => (float)m.RuntimeMinutes!.Value).ToList();
                if (runtimes.Any()) profile.PreferredRuntimeMax = runtimes.Average();
            }

            profile.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // ✅ Invalide le cache après recalcul complet
            _cache.Remove($"profile:{userId}");
        }

        // ─── Profil série — swipe (signal immédiat) ────────────────────

        public async Task<UserSeriesProfile> GetOrCreateSeriesProfileAsync(
            Guid userId, AppDbContext? db = null)
        {
            var cacheKey = $"seriesprofile:{userId}";
            if (_cache.TryGetValue(cacheKey, out UserSeriesProfile? cached)) return cached!;

            db ??= _db;
            var profile = await db.UserSeriesProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile is null)
            {
                profile = new UserSeriesProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    UpdatedAt = DateTime.UtcNow
                };
                db.UserSeriesProfiles.Add(profile);
                await db.SaveChangesAsync();
            }

            _cache.Set(cacheKey, profile, TimeSpan.FromMinutes(5));
            return profile;
        }

        public async Task UpdateSeriesProfileAsync(
            Guid userId, Guid seriesId, SwipeDirection direction, int durationMs,
            AppDbContext? db = null)
        {
            db ??= _db;

            var profile = await GetOrCreateSeriesProfileAsync(userId, db);
            var series = await db.Series.FindAsync(seriesId);
            if (series is null) return;

            float signal = direction == SwipeDirection.Right
                ? durationMs < 1500 ? 0.4f : 0.3f
                : durationMs < 1500 ? -0.28f : -0.12f;

            UpdateWeightsIncremental(series.Genres, signal, profile.GenreWeights, profile.GenreCounts);
            UpdateWeightsIncremental(series.CreatedBy, signal, profile.CreatorWeights, profile.CreatorCounts);
            UpdateWeightsIncremental(series.CastTop5, signal, profile.ActorWeights, profile.ActorCounts);
            UpdateWeightsIncremental(series.Keywords, signal, profile.KeywordWeights, profile.KeywordCounts);

            profile.TotalSeriesSignals++;
            profile.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            _cache.Remove($"seriesprofile:{userId}");
        }

        /// <summary>Même principe que FireAndForgetProfileUpdate, pour les séries.</summary>
        public void FireAndForgetSeriesProfileUpdate(
            Guid userId, Guid seriesId, SwipeDirection direction, int durationMs)
        {
            _ = Task.Run(async () =>
            {
                await using var db = await _dbFactory.CreateDbContextAsync();
                await UpdateSeriesProfileAsync(userId, seriesId, direction, durationMs, db);
            });
        }

        // ─── Profil série — sync (signal périodique, basé sur les saisons) ──

        /// <summary>
        /// Recalcule le profil série à partir de l'agrégat des saisons possédées/
        /// regardées (SeriesWatchHistory + ServerSeriesSeason) et des swipes sur
        /// des séries entières — même principe à deux vitesses que UpdateProfileAsync/
        /// UpdateProfileFromHistoryAsync pour les films, cf. le plan.
        /// </summary>
        public async Task UpdateSeriesProfileFromHistoryAsync(Guid userId)
        {
            var profile = await GetOrCreateSeriesProfileAsync(userId);

            profile.GenreWeights = new Dictionary<string, float>();
            profile.CreatorWeights = new Dictionary<string, float>();
            profile.ActorWeights = new Dictionary<string, float>();
            profile.KeywordWeights = new Dictionary<string, float>();
            profile.GenreCounts = new Dictionary<string, int>();
            profile.CreatorCounts = new Dictionary<string, int>();
            profile.ActorCounts = new Dictionary<string, int>();
            profile.KeywordCounts = new Dictionary<string, int>();
            profile.TotalSeriesSignals = 0;

            var history = await _db.SeriesWatchHistory
                .Include(w => w.SeriesSeason).ThenInclude(s => s.Series)
                .Where(w => w.UserId == userId)
                .ToListAsync();

            const float completionThreshold = 70f;

            var seasonsRatios = new List<float>();
            var completionPcts = new List<float>();

            foreach (var group in history.GroupBy(w => w.SeriesSeason.SeriesId))
            {
                var series = group.First().SeriesSeason.Series;
                var watchedRows = group.ToList();

                var possessedCount = await _db.ServerSeriesSeasons
                    .Where(sss => sss.SeriesSeason.SeriesId == series.Id)
                    .Select(sss => sss.SeriesSeasonId)
                    .Distinct()
                    .CountAsync();

                var watchedCount = watchedRows.Count(w => w.CompletionPct >= completionThreshold);
                var seasonsRatio = watchedCount / (float)Math.Max(possessedCount, Math.Max(watchedCount, 1));

                var avgCompletion = watchedRows.Average(w => w.CompletionPct);
                var anyFavorite = watchedRows.Any(w => w.IsFavorite);

                seasonsRatios.Add(seasonsRatio);
                completionPcts.Add(avgCompletion);

                var combined = seasonsRatio * 0.6f + (avgCompletion / 100f) * 0.4f;
                float engagementScore = combined switch
                {
                    >= 0.9f => 1.0f,
                    >= 0.7f => 0.75f,
                    >= 0.4f => 0.3f,
                    >= 0.2f => 0.1f,
                    _ => 0.0f
                };
                if (anyFavorite) engagementScore = MathF.Min(engagementScore + 0.2f, 1f);

                if (combined < 0.05f && !anyFavorite) continue;

                UpdateWeightsIncremental(series.Genres, engagementScore, profile.GenreWeights, profile.GenreCounts);
                UpdateWeightsIncremental(series.CreatedBy, engagementScore, profile.CreatorWeights, profile.CreatorCounts);
                UpdateWeightsIncremental(series.CastTop5, engagementScore, profile.ActorWeights, profile.ActorCounts);
                UpdateWeightsIncremental(series.Keywords, engagementScore, profile.KeywordWeights, profile.KeywordCounts);

                profile.TotalSeriesSignals++;
            }

            profile.AvgSeasonCompletionRate = completionPcts.Any() ? completionPcts.Average() : 0f;
            profile.AvgSeasonsWatchedRatio = seasonsRatios.Any() ? seasonsRatios.Average() : 0f;

            var swipes = await _db.Swipes
                .Include(s => s.Series)
                .Where(s => s.UserId == userId && s.Series != null)
                .ToListAsync();

            foreach (var swipe in swipes)
            {
                if (swipe.Series is null) continue;

                float signal = swipe.Direction == SwipeDirection.Right
                    ? swipe.DurationMs < 1500 ? 0.4f : 0.3f
                    : swipe.DurationMs < 1500 ? -0.2f : -0.12f;

                UpdateWeightsIncremental(swipe.Series.Genres, signal, profile.GenreWeights, profile.GenreCounts);
                UpdateWeightsIncremental(swipe.Series.CreatedBy, signal, profile.CreatorWeights, profile.CreatorCounts);
                UpdateWeightsIncremental(swipe.Series.CastTop5, signal, profile.ActorWeights, profile.ActorCounts);
                UpdateWeightsIncremental(swipe.Series.Keywords, signal, profile.KeywordWeights, profile.KeywordCounts);

                profile.TotalSeriesSignals++;
            }

            profile.GenreWeights = Normalize(profile.GenreWeights, profile.GenreCounts);
            profile.CreatorWeights = Normalize(profile.CreatorWeights, profile.CreatorCounts);
            profile.ActorWeights = Normalize(profile.ActorWeights, profile.ActorCounts);
            profile.KeywordWeights = Normalize(profile.KeywordWeights, profile.KeywordCounts);
            profile.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            _cache.Remove($"seriesprofile:{userId}");
        }

        // ─── Helpers ──────────────────────────────────────────────────

        private void UpdateWeightsIncremental(
            string[] items, float signal,
            Dictionary<string, float> weights,
            Dictionary<string, int> counts)
        {
            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item)) continue;

                var n = counts.GetValueOrDefault(item, 0) + 1;
                var lr = 1f / MathF.Sqrt(n); // learning rate décroissant
                var current = weights.GetValueOrDefault(item, 0f);

                weights[item] = current + lr * (signal - current);
                counts[item] = n;
            }
        }

        private Dictionary<string, float> Normalize(
            Dictionary<string, float> weights,
            Dictionary<string, int>? counts = null)
        {
            if (!weights.Any()) return weights;

            // Pondère chaque valeur par la confiance (log du nombre de signaux)
            var weighted = weights.ToDictionary(k => k.Key, k =>
            {
                var confidence = counts != null && counts.TryGetValue(k.Key, out var n)
                    ? MathF.Log(1 + n)  // log(1) = 0, log(2) ≈ 0.69, log(11) ≈ 2.4
                    : 1f;
                return k.Value * confidence;
            });

            var maxPos = weighted.Values.Where(v => v > 0).DefaultIfEmpty(1f).Max();
            var minNeg = weighted.Values.Where(v => v < 0).DefaultIfEmpty(-1f).Min();

            return weighted.ToDictionary(k => k.Key, v =>
            {
                if (v.Value > 0) return v.Value / maxPos;
                if (v.Value < 0) return v.Value / Math.Abs(minNeg);
                return 0f;
            });
        }

        private float CosineSimilarity(
            Dictionary<string, float> a, Dictionary<string, float> b)
        {
            var common = a.Keys.Intersect(b.Keys).ToList();
            if (!common.Any()) return 0f;
            float dot = common.Sum(k => a[k] * b[k]);
            float normA = MathF.Sqrt(a.Values.Sum(v => v * v));
            float normB = MathF.Sqrt(b.Values.Sum(v => v * v));
            return normA == 0 || normB == 0 ? 0f : dot / (normA * normB);
        }
    }

    // ─── Supporting types ─────────────────────────────────────────────

    public record ScoredMovie(Movie Movie, float Score);
    public record ScoredSeries(Series Series, float Score);

    public enum RecommendationContext { Solo, Group, Evening, Discovery, Roulette }

    public class AlgoWeights
    {
        public float History { get; set; }
        public float ContentBased { get; set; }
        public float Collaborative { get; set; }
        public float Popularity { get; set; }
        public float Available { get; set; }
    }

    public class HomeSection
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public List<HomeSectionMovie> Movies { get; set; } = new();
    }

    public class HomePageResult
    {
        public List<HomeSection> Sections { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public bool HasMore { get; set; }
    }

    public class HomeSectionMovie
    {
        public Guid Id { get; set; }
        public int TmdbId { get; set; }
        public string Title { get; set; } = "";
        public string? PosterPath { get; set; }
        public string? BackdropPath { get; set; }
        public string? Overview { get; set; }
        public float TmdbRating { get; set; }
        public int? RuntimeMinutes { get; set; }
        public string[] Genres { get; set; } = Array.Empty<string>();
        public DateOnly? ReleaseDate { get; set; }
        public string ContentType { get; set; } = "movie";
        public bool IsAvailable { get; set; }
        public float Score { get; set; }
    }
}