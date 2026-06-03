// swipefilm/Controllers/RecommendationController.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using swipefilm.Auth;
using swipefilm.Auth;

namespace swipefilm.Controllers
{
    [ApiController]
    [Route("api/recommendations")]
    [Authorize]
    public class RecommendationController : ControllerBase
    {
        private readonly RecommendationEngine _engine;

        public RecommendationController(RecommendationEngine engine)
        {
            _engine = engine;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet("{serverId}")]
        public async Task<IActionResult> GetRecommendations(
            Guid serverId,
            [FromQuery] RecommendationContext context = RecommendationContext.Solo,
            [FromQuery] int count = 20)
        {
            var (movies, available) = await _engine
                .GetRecommendationsAsync(CurrentUserId, serverId, context, count);

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
                Score = Math.Round(r.Score, 3)
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
            await _engine.UpdateProfileFromHistoryAsync(CurrentUserId);
            return Ok(new { message = "Profil mis à jour ✅" });
        }
    }
}