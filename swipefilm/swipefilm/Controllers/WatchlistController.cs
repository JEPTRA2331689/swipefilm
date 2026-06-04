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

        [HttpGet("{serverId}")]
        public async Task<IActionResult> GetWatchlist(Guid serverId)
        {
            // ✅ Films swipés à droite
            var swipedRight = await _db.Swipes
                .Include(s => s.Movie)
                .Where(s => s.UserId == CurrentUserId
                         && s.Direction == SwipeDirection.Right
                         && s.Movie != null)
                .OrderByDescending(s => s.CreatedAt)
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
                    s.Movie.ReleaseDate,
                    s.Movie.ContentType,
                    SwipedAt = s.CreatedAt,
                    IsAvailable = _db.ServerMovie
                        .Any(sm => sm.ServerId == serverId
                               && sm.MovieId == s.Movie!.Id),
                })
                .ToListAsync();

            return Ok(swipedRight);
        }

        [HttpDelete("{movieId}")]
        public async Task<IActionResult> RemoveFromWatchlist(Guid movieId)
        {
            // Supprime le swipe droit pour retirer de la watchlist
            var swipe = await _db.Swipes
                .FirstOrDefaultAsync(s => s.UserId == CurrentUserId
                                       && s.MovieId == movieId
                                       && s.Direction == SwipeDirection.Right);

            if (swipe is null) return NotFound();

            _db.Swipes.Remove(swipe);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Retiré de la watchlist ✅" });
        }
    }
}
