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
    [Route("api/watchlist")]
    [Authorize]
    public class WatchlistController : ControllerBase
    {
        private readonly AppDbContext _db;
        public WatchlistController(AppDbContext db) => _db = db;

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<IActionResult> GetWatchlist()
        {
            // ✅ Films swipés à droite
            var swipedMovies = await _db.Swipes
                .Include(s => s.Movie)
                .Where(s => s.UserId == CurrentUserId
                         && s.Direction == SwipeDirection.Right
                         && s.Movie != null)
                .Select(s => new
                {
                    s.Movie!.Id,
                    s.Movie.TmdbId,
                    s.Movie.Title,
                    s.Movie.PosterPath,
                    s.Movie.Overview,
                    s.Movie.TmdbRating,
                    s.Movie.RuntimeMinutes,
                    s.Movie.Genres,
                    ReleaseDate = s.Movie.ReleaseDate,
                    s.Movie.ContentType,
                    SwipedAt = s.CreatedAt,
                    IsAvailable = _db.ServerMovie
                        .Any(sm => sm.MovieId == s.Movie!.Id),
                })
                .ToListAsync();

            // ✅ Séries swipées à droite — manquait entièrement avant ce correctif
            var swipedSeries = await _db.Swipes
                .Include(s => s.Series)
                .Where(s => s.UserId == CurrentUserId
                         && s.Direction == SwipeDirection.Right
                         && s.Series != null)
                .Select(s => new
                {
                    s.Series!.Id,
                    s.Series.TmdbId,
                    s.Series.Title,
                    s.Series.PosterPath,
                    s.Series.Overview,
                    s.Series.TmdbRating,
                    RuntimeMinutes = (int?)null,
                    s.Series.Genres,
                    ReleaseDate = s.Series.FirstAirDate,
                    ContentType = "series",
                    SwipedAt = s.CreatedAt,
                    IsAvailable = _db.ServerSeries
                        .Any(ss => ss.SeriesId == s.Series!.Id),
                })
                .ToListAsync();

            // ✅ Mêmes noms/types de propriétés dans le même ordre des deux côtés
            // → même type anonyme généré par le compilateur, Concat direct.
            var merged = swipedMovies
                .Concat(swipedSeries)
                .OrderByDescending(w => w.SwipedAt)
                .ToList();

            return Ok(merged);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> RemoveFromWatchlist(Guid id)
        {
            // Supprime le swipe droit pour retirer de la watchlist — film ou série
            var swipe = await _db.Swipes
                .FirstOrDefaultAsync(s => s.UserId == CurrentUserId
                                       && s.Direction == SwipeDirection.Right
                                       && (s.MovieId == id || s.SeriesId == id));

            if (swipe is null) return NotFound();

            _db.Swipes.Remove(swipe);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Retiré de la watchlist ✅" });
        }
    }
}
