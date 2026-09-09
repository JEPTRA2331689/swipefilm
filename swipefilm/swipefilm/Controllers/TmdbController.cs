using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using swipefilm.Auth.swipefilm.Services;
using swipefilm.Data;

namespace swipefilm.Controllers
{
    [ApiController]
    [Route("api/tmdb")]
    [Authorize]
    public class TmdbController : ControllerBase
    {
        private readonly TmdbService _tmdb;
        private readonly AppDbContext _db;

        public TmdbController(TmdbService tmdb, AppDbContext db)
        {
            _tmdb = tmdb;
            _db = db;
        }

        [HttpPost("enrich")]
        public async Task<IActionResult> EnrichAll()
        {
            await _tmdb.EnrichAllMoviesAsync();
            return Ok(new { message = "Enrichissement TMDB terminé ✅" });
        }

        /// <summary>
        /// Fallback quand le moteur de reco n'a rien à proposer (catalogue
        /// vide/utilisateur neuf), et décor de fond des pages login/signup/
        /// onboarding — donc public, appelé AVANT authentification.
        /// </summary>
        [HttpGet("popular")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPopular([FromQuery] int page = 1)
        {
            var posters = await _tmdb.GetPopularMoviesAsync(page);
            return Ok(new { posters });
        }

        /// <summary>
        /// Complément à GET /api/movies/{id} — cast avec photos + tagline,
        /// que MovieController ne renvoie pas.
        /// </summary>
        [HttpGet("movie/{tmdbId:int}")]
        public async Task<IActionResult> GetMovieExtra(int tmdbId)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _db.Users.FindAsync(userId);
            var locale = $"{user?.Locale ?? "fr"}-{user?.Region ?? "FR"}";

            var extra = await _tmdb.GetMovieExtraAsync(tmdbId, locale);
            if (extra is null) return NotFound();

            return Ok(extra);
        }
    }
}
