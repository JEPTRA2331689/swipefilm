// swipefilm/Controllers/SonarrWebhookController.cs
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using swipefilm.Auth;
using swipefilm.Data;
using swipefilm.Models;

namespace swipefilm.Controllers
{
    /// <summary>
    /// Reçoit les événements "Connect → Webhook" de Sonarr (Grab/Download) pour
    /// mettre à jour le statut des requêtes en temps réel — même principe que
    /// RadarrWebhookController. Pas de [Authorize] — Sonarr ne sait pas envoyer
    /// un Bearer JWT — la sécurité passe par le token en query string.
    /// </summary>
    [ApiController]
    [Route("api/webhooks/sonarr")]
    public class SonarrWebhookController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IAppConfigService _config;
        private readonly ILogger<SonarrWebhookController> _logger;

        public SonarrWebhookController(
            AppDbContext db, IAppConfigService config, ILogger<SonarrWebhookController> logger)
        {
            _db = db;
            _config = config;
            _logger = logger;
        }

        // ✅ Même raisonnement que RadarrWebhookController.Ping — sans handler
        // GET explicite, une visite navigateur retombe sur le MapFallback du
        // frontend (page web) au lieu d'un message clair. N'affecte pas
        // Sonarr, qui n'envoie que du POST.
        [HttpGet]
        public IActionResult Ping() =>
            Ok(new { message = "Endpoint webhook Sonarr — attend un POST de Sonarr, pas une visite navigateur." });

        [HttpPost]
        public async Task<IActionResult> Receive(
            [FromQuery] string? token, [FromBody] JsonElement body)
        {
            var expected = _config.GetSonarr()?.WebhookToken;
            if (string.IsNullOrEmpty(expected) || token != expected)
                return Unauthorized();

            var eventType = body.TryGetProperty("eventType", out var et) ? et.GetString() : null;

            // ✅ Bouton "Test" de Sonarr — répond OK sans rien faire d'autre
            if (eventType == "Test") return Ok();

            // ✅ "Grab" (release trouvée) et "Download" (épisode importé) ne
            // veulent dire que ça — pas que Jellyfin/Plex l'a scanné. Le
            // passage à Available/PartiallyAvailable n'est confirmé que par
            // ConfirmAvailabilityAsync (sync Jellyfin/Plex).
            var newStatus = eventType switch
            {
                "Grab" or "Download" => RequestStatus.Downloading,
                _ => (RequestStatus?)null,
            };

            if (newStatus is null) return Ok();

            if (!body.TryGetProperty("series", out var seriesEl) ||
                !seriesEl.TryGetProperty("tvdbId", out var tvdbProp))
                return Ok();

            var tvdbId = tvdbProp.GetInt32();

            var series = await _db.Series.FirstOrDefaultAsync(s => s.TvdbId == tvdbId);
            if (series is null) return Ok();

            var requests = await _db.MediaRequests
                .Where(r => r.SeriesId == series.Id && r.Status == RequestStatus.Approved)
                .ToListAsync();

            if (!requests.Any()) return Ok();

            foreach (var req in requests)
                req.Status = newStatus.Value;

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "[Webhook Sonarr] {Event} → {Count} requête(s) mises à jour pour tvdbId {TvdbId}",
                eventType, requests.Count, tvdbId);

            return Ok();
        }
    }
}
