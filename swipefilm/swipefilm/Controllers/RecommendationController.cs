// swipefilm/Controllers/RecommendationController.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using swipefilm.Auth;
using swipefilm.Data;
using swipefilm.Models;

namespace swipefilm.Controllers
{
    [ApiController]
    [Route("api/recommendations")]
    [RequirePermission(Permission.CanSwipe)]
    public class RecommendationController : ControllerBase
    {
        private readonly RecommendationEngine _engine;
        private readonly AppDbContext _db;
        private readonly IAppConfigService _config;

        public RecommendationController(
            RecommendationEngine engine, AppDbContext db, IAppConfigService config)
        {
            _engine = engine;
            _db = db;
            _config = config;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<IActionResult> GetRecommendations(
             [FromQuery] string section = "for_you",
             [FromQuery] int count = 20,
             [FromQuery] Guid? basedOnMovieId = null,
             [FromQuery] AvailabilityFilter availability = AvailabilityFilter.All,
             [FromQuery] string? excludeIds = null) // ✅ nouveau — liste de TmdbIds séparés par virgule

        {
            var excludedTmdbIds = excludeIds?
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), out var id) ? id : -1)
                .Where(id => id > 0)
                .ToHashSet() ?? new HashSet<int>();
            // ✅ Résout le profil selon l'ID de section
            var profile = section switch
            {
                "for_you" => SectionProfile.ForYou,
                "daily_discovery" => SectionProfile.DailyDiscovery,
                "because_you_liked" => basedOnMovieId.HasValue
                                        ? SectionProfile.BecauseYouLiked(basedOnMovieId.Value)
                                        : SectionProfile.ForYou,
                "favorite_actors" => SectionProfile.FavoriteActors,
                "favorite_directors" => SectionProfile.FavoriteDirectors,
                "hidden_gems" => SectionProfile.HiddenGemsList,
                "weekly_discovery" => SectionProfile.WeeklyDiscovery,
                "surprise_me" => SectionProfile.SurpriseMe,
                "recent_releases" => SectionProfile.RecentReleases,
                _ => SectionProfile.ForYou,
            };


            var (movies, available) = await _engine
                .GetRecommendationsAsync(CurrentUserId, profile, count, availability, excludedTmdbIds);

            // ✅ Lien direct Jellyfin/Plex — même logique que MovieController.GetMovie,
            // mais en une seule requête pour tout le lot plutôt qu'un aller-retour
            // par film (sinon N+1 sur chaque page de recommandations).
            var movieIds = movies.Select(r => r.Movie.Id).ToList();
            var serverItemIds = await _db.ServerMovie
                .Where(sm => movieIds.Contains(sm.MovieId))
                .ToDictionaryAsync(sm => sm.MovieId, sm => sm.ServerItemId);
            var serverConfig = _config.GetServer();

            return Ok(movies.Select(r =>
            {
                string? jellyfinUrl = null;
                string? plexUrl = null;

                if (serverConfig is not null
                    && serverItemIds.TryGetValue(r.Movie.Id, out var serverItemId))
                {
                    var url = ExternalLinkHelper.BuildDeepLink(
                        serverConfig.Type, serverConfig.Url,
                        serverConfig.MachineIdentifier, serverItemId);

                    if (serverConfig.Type == ServerType.Jellyfin) jellyfinUrl = url;
                    else plexUrl = url;
                }

                return new
                {
                    r.Movie.Id,
                    r.Movie.TmdbId,
                    r.Movie.Title,
                    r.Movie.PosterPath,
                    r.Movie.Overview,
                    r.Movie.TmdbRating,
                    r.Movie.RuntimeMinutes,
                    r.Movie.Genres,
                    r.Movie.ReleaseDate,
                    r.Movie.ContentType,
                    IsAvailable = available.Contains(r.Movie.TmdbId),
                    JellyfinUrl = jellyfinUrl,
                    PlexUrl = plexUrl,
                    Score = Math.Round(r.Score, 3),
                    SectionId = profile.Id,    // ← utile pour le debug Flutter
                    SectionTitle = profile.Title
                };
            }));
        }

        // ✅ Résout un id de section (statique, "genre_{genre}", ou son
        // équivalent "_series") en SectionProfile + indicateur film/série —
        // même table que BuildSectionDescriptors côté RecommendationEngine,
        // dupliquée ici volontairement : cette résolution part d'une string
        // publique (id cliqué côté client), contrairement à la génération de
        // la home qui part des profils utilisateur.
        private static (SectionProfile Profile, bool IsSeries) ResolveSectionProfile(string sectionId)
        {
            var isSeries = sectionId.EndsWith("_series");
            var baseId = isSeries ? sectionId[..^"_series".Length] : sectionId;

            if (baseId.StartsWith("genre_"))
            {
                var genre = baseId["genre_".Length..];
                var genreProfile = SectionProfile.ForGenre(genre);
                return (isSeries ? genreProfile with { Id = sectionId } : genreProfile, isSeries);
            }

            var resolved = baseId switch
            {
                "for_you" => SectionProfile.ForYou,
                "hidden_gems" => SectionProfile.HiddenGemsList,
                "surprise_me" => SectionProfile.SurpriseMe,
                "recent_releases" => SectionProfile.RecentReleases,
                "daily_discovery" => SectionProfile.DailyDiscovery,
                "weekly_discovery" => SectionProfile.WeeklyDiscovery,
                "favorite_actors" => SectionProfile.FavoriteActors,
                "favorite_directors" => SectionProfile.FavoriteDirectors,
                "seasonal" => SectionProfile.Seasonal() ?? SectionProfile.ForYou,
                _ => SectionProfile.ForYou,
            };

            return (isSeries ? resolved with { Id = sectionId } : resolved, isSeries);
        }

        private static HashSet<int> ParseExcludeIds(string? excludeIds) =>
            excludeIds?
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), out var id) ? id : -1)
                .Where(id => id > 0)
                .ToHashSet() ?? new HashSet<int>();

        /// <summary>"Voir plus" — reprend une section précise (algorithmique ou
        /// par genre, film ou série) avec un lot supplémentaire, en excluant ce
        /// qui a déjà été vu côté client (même principe que /api/recommendations/home).</summary>
        [HttpGet("browse")]
        public async Task<IActionResult> Browse(
            [FromQuery] string sectionId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 24,
            [FromQuery] AvailabilityFilter availability = AvailabilityFilter.All,
            [FromQuery] string? excludeIds = null)
        {
            var (profile, isSeries) = ResolveSectionProfile(sectionId);
            var excluded = ParseExcludeIds(excludeIds);

            var section = await _engine.GetSectionPageAsync(
                CurrentUserId, profile, pageSize, isSeries, availability, excluded);

            var movies = section?.Movies ?? [];
            return Ok(new
            {
                Title = section?.Title ?? profile.Title,
                Movies = movies,
                HasMore = movies.Count >= pageSize,
            });
        }

        /// <summary>Exploration d'un genre façon Overseerr — mélange films et
        /// séries de ce genre plutôt que deux listes séparées.</summary>
        [HttpGet("browse-genre")]
        public async Task<IActionResult> BrowseGenre(
            [FromQuery] string genre,
            [FromQuery] int pageSize = 24,
            [FromQuery] AvailabilityFilter availability = AvailabilityFilter.All,
            [FromQuery] HomeContentFilter contentType = HomeContentFilter.All,
            [FromQuery] string? excludeIds = null)
        {
            var excluded = ParseExcludeIds(excludeIds);
            var wantMovies = contentType != HomeContentFilter.SeriesOnly;
            var wantSeries = contentType != HomeContentFilter.MoviesOnly;
            // ✅ Filtré sur un seul type : ce type prend toute la page plutôt
            // que de garder une moitié de quota inutilisée.
            var half = wantMovies && wantSeries ? Math.Max(1, pageSize / 2) : pageSize;

            var movieSection = wantMovies
                ? await _engine.GetSectionPageAsync(
                    CurrentUserId, SectionProfile.ForGenre(genre), half, isSeries: false,
                    availability, excluded)
                : null;
            var seriesSection = wantSeries
                ? await _engine.GetSectionPageAsync(
                    CurrentUserId,
                    SectionProfile.ForGenre(genre) with { Id = $"genre_series_{genre}" },
                    half, isSeries: true, availability, excluded)
                : null;

            var movies = movieSection?.Movies ?? [];
            var series = seriesSection?.Movies ?? [];

            // ✅ Entrelacé plutôt que concaténé — évite "tous les films puis
            // toutes les séries" dans la grille d'exploration.
            var mixed = new List<HomeSectionMovie>();
            for (int i = 0; i < Math.Max(movies.Count, series.Count); i++)
            {
                if (i < movies.Count) mixed.Add(movies[i]);
                if (i < series.Count) mixed.Add(series[i]);
            }

            return Ok(new
            {
                Title = $"{genre}",
                Movies = mixed,
                HasMore = movies.Count >= half || series.Count >= half,
            });
        }

        /// <summary>Liste des genres disponibles avec un backdrop représentatif
        /// — alimente le carrousel de genres façon Overseerr.</summary>
        [HttpGet("genres")]
        public async Task<IActionResult> GetGenres()
        {
            var genres = await _engine.GetGenreCardsAsync();
            return Ok(genres.Select(g => new { g.Genre, g.BackdropPath }));
        }

        /// <summary>"Films/séries similaires" — même mécanisme que la home
        /// ("Parce que vous avez aimé X"), exposé pour un film OU une série
        /// pivot. Contrairement à /api/recommendations (movie-only), celui-ci
        /// couvre les deux via GetSectionPageAsync.</summary>
        [HttpGet("similar")]
        public async Task<IActionResult> GetSimilar(
            [FromQuery] Guid id,
            [FromQuery] bool isSeries = false,
            [FromQuery] string? title = null,
            [FromQuery] int count = 12)
        {
            var profile = SectionProfile.BecauseYouLiked(id, title);
            var section = await _engine.GetSectionPageAsync(CurrentUserId, profile, count, isSeries);
            return Ok(section?.Movies ?? []);
        }

        [HttpPost("discover")]
        public async Task<IActionResult> Discover(
            [FromServices] PersonalizedDiscoveryService discovery)
        {
            await discovery.DiscoverForUserAsync(CurrentUserId);
            return Ok(new { message = "Découverte personnalisée lancée ✅" });
        }

        [HttpPost("profile/update")]
        public async Task<IActionResult> UpdateProfile()
        {
            // ⚠️ Séquentiel, pas Task.WhenAll : les deux méthodes partagent le
            // même AppDbContext scoped (_db) côté RecommendationEngine, qui
            // n'est pas thread-safe pour des opérations concurrentes.
            await _engine.UpdateProfileFromHistoryAsync(CurrentUserId);
            await _engine.UpdateSeriesProfileFromHistoryAsync(CurrentUserId);

            return Ok(new { message = "Profils (films + séries) mis à jour ✅" });
        }
        [HttpGet("home")]
        public async Task<IActionResult> GetHomePage(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 5,
            [FromQuery] int countPerSection = 20,
            [FromQuery] AvailabilityFilter availability = AvailabilityFilter.All,
            [FromQuery] HomeContentFilter contentType = HomeContentFilter.All,
            [FromQuery] string? excludeIds = null) // ✅ TmdbIds déjà vus, accumulés côté client au fil du scroll
        {
            var excludeTmdbIds = excludeIds?
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), out var id) ? id : -1)
                .Where(id => id > 0)
                .ToHashSet() ?? new HashSet<int>();

            var result = await _engine.GetHomePageAsync(
                CurrentUserId, page, pageSize, countPerSection, availability, contentType, excludeTmdbIds);

            return Ok(result);
        }

    }
}