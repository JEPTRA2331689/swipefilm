// swipefilm/Controllers/SwipeController.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using swipefilm.Auth;
using swipefilm.Data;
using swipefilm.Models;
using swipefilm.Auth;

namespace swipefilm.Controllers
{
    [ApiController]
    [Route("api/swipe")]
    [Authorize]
    public class SwipeController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly RecommendationEngine _engine;

        public SwipeController(AppDbContext db, RecommendationEngine engine)
        {
            _db = db;
            _engine = engine;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpPost]
        public async Task<IActionResult> Swipe([FromBody] SwipeDto dto)
        {
            var movie = await _db.Movies.FindAsync(dto.MovieId);
            if (movie is null) return NotFound();

            _db.Swipes.Add(new Swipe
            {
                Id = Guid.NewGuid(),
                UserId = CurrentUserId,
                MovieId = dto.MovieId,
                Direction = dto.Direction,
                DurationMs = dto.DurationMs,
                ContextMode = dto.Context,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            // ✅ Tous les paramètres passés correctement
            var userId = CurrentUserId;
            var movieId = dto.MovieId;
            var direction = dto.Direction;
            var durationMs = dto.DurationMs;

            _ = Task.Run(async () =>
                await _engine.UpdateProfileAsync(userId, movieId, direction, durationMs));

            return Ok(new { message = "Swipe enregistré ✅" });
        }
    }

    public record SwipeDto(
        Guid MovieId,
        SwipeDirection Direction,
        int DurationMs,
        SwipeContext Context
    );
}