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
            if ((dto.MovieId is null) == (dto.SeriesId is null))
                return BadRequest(new { error = "Il faut renseigner soit MovieId soit SeriesId, jamais les deux ni aucun" });

            if (dto.MovieId.HasValue && await _db.Movies.FindAsync(dto.MovieId.Value) is null)
                return NoContent();
            if (dto.SeriesId.HasValue && await _db.Series.FindAsync(dto.SeriesId.Value) is null)
                return NoContent();

            _db.Swipes.Add(new Swipe
            {
                Id = Guid.NewGuid(),
                UserId = CurrentUserId,
                MovieId = dto.MovieId,
                SeriesId = dto.SeriesId,
                Direction = dto.Direction,
                DurationMs = dto.DurationMs,
                ContextMode = dto.Context,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            var userId = CurrentUserId;
            var direction = dto.Direction;
            var durationMs = dto.DurationMs;

            if (dto.MovieId.HasValue)
                _engine.FireAndForgetProfileUpdate(userId, dto.MovieId.Value, direction, durationMs);
            else
                _engine.FireAndForgetSeriesProfileUpdate(userId, dto.SeriesId!.Value, direction, durationMs);

            return Ok(new { message = "Swipe enregistré ✅" });
        }
    }

    public record SwipeDto(
        Guid? MovieId,
        Guid? SeriesId,
        SwipeDirection Direction,
        int DurationMs,
        SwipeContext Context
    );
}