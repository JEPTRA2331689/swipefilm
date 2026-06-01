// swipefilm/Controllers/OverseerrController.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using swipefilm.Auth;
using swipefilm.Data;
using swipefilm.Models;
using swipefilm.Auth;

namespace swipefilm.Controllers
{
    [ApiController]
    [Route("api/seerr")]
    [Authorize]
    public class SeerrController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly SeerrService _seerr;

        public SeerrController(AppDbContext db, SeerrService seerr)
        {
            _db = db;
            _seerr = seerr;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // ─── Configurer Seerr ─────────────────────────────────────────

        [HttpPost("configure")]
        public async Task<IActionResult> Configure([FromBody] ConfigureSeerrDto dto)
        {
            // Valider que Seerr répond
            using var http = new HttpClient();
            var testReq = new HttpRequestMessage(
                HttpMethod.Get, $"{dto.Url}/api/v1/settings/main");
            testReq.Headers.Add("X-Api-Key", dto.ApiKey);

            var testResp = await http.SendAsync(testReq);
            if (!testResp.IsSuccessStatusCode)
                return BadRequest("Impossible de contacter Seerr — vérifie l'URL et l'API Key");

            var existing = await _db.UserSeerr
                .FirstOrDefaultAsync(o => o.UserId == CurrentUserId);

            var encryptionService = HttpContext.RequestServices
                .GetRequiredService<IEncryptionService>();

            if (existing is null)
            {
                _db.UserSeerr.Add(new UserSeerr
                {
                    Id = Guid.NewGuid(),
                    UserId = CurrentUserId,
                    UrlEncrypted = encryptionService.Encrypt(dto.Url),
                    ApiKeyEncrypted = encryptionService.Encrypt(dto.ApiKey),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.UrlEncrypted = encryptionService.Encrypt(dto.Url);
                existing.ApiKeyEncrypted = encryptionService.Encrypt(dto.ApiKey);
                existing.IsActive = true;
            }

            await _db.SaveChangesAsync();
            return Ok(new { message = "Seerr configuré ✅" });
        }

        // ─── Requêter un film ─────────────────────────────────────────

        [HttpPost("request/{tmdbId}")]
        public async Task<IActionResult> RequestMedia(
            int tmdbId, [FromQuery] string mediaType = "movie")
        {
            var overseerr = await _db.UserSeerr
                .FirstOrDefaultAsync(o =>
                    o.UserId == CurrentUserId && o.IsActive);

            if (overseerr is null)
                return BadRequest("Seerr non configuré");

            try
            {
                var result = await _seerr.RequestMediaAsync(
                    CurrentUserId, overseerr.Id, tmdbId, mediaType);

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // ─── Statut d'un film dans Seerr ──────────────────────────────

        [HttpGet("status/{tmdbId}")]
        public async Task<IActionResult> GetMediaStatus(
            int tmdbId, [FromQuery] string mediaType = "movie")
        {
            var overseerr = await _db.UserSeerr     
                .FirstOrDefaultAsync(o =>
                    o.UserId == CurrentUserId && o.IsActive);

            if (overseerr is null)
                return BadRequest("Seerr non configuré");

            var availability = await _seerr.CheckMediaAsync(
                overseerr.Id, tmdbId, mediaType);

            return Ok(availability);
        }

        // ─── Mes requêtes ─────────────────────────────────────────────

        [HttpGet("requests")]
        public async Task<IActionResult> GetMyRequests()
        {
            var requests = await _db.Requests
                .Include(r => r.Movie)
                .Where(r => r.UserId == CurrentUserId)
                .OrderByDescending(r => r.RequestedAt)
                .Select(r => new
                {
                    r.Id,
                    r.OverseerrRequestId,
                    r.Status,
                    r.RequestedAt,
                    r.AvailableAt,
                    Movie = new
                    {
                        r.Movie.TmdbId,
                        r.Movie.Title,
                        r.Movie.PosterPath
                    }
                })
                .ToListAsync();

            return Ok(requests);
        }

        // ─── Sync statuts ─────────────────────────────────────────────

        [HttpPost("sync")]
        public async Task<IActionResult> SyncStatuses()
        {
            await _seerr.SyncRequestStatusesAsync(CurrentUserId);
            return Ok(new { message = "Statuts synchronisés ✅" });
        }
    }

    public record ConfigureSeerrDto(string Url, string ApiKey);
}