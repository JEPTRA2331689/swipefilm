// swipefilm/Controllers/RecommendationController.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using swipefilm.Auth;
using swipefilm.Models;

namespace swipefilm.Controllers
{
    [ApiController]
    [Route("api/recommendations")]
    [RequirePermission(Permission.CanSwipe)]
    public class RecommendationController : ControllerBase
    {
        private readonly RecommendationEngine _engine;

        public RecommendationController(RecommendationEngine engine)
        {
            _engine = engine;
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

            return Ok(movies.Select(r => new
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
                Score = Math.Round(r.Score, 3),
                SectionId = profile.Id,    // ← utile pour le debug Flutter
                SectionTitle = profile.Title
            }));
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