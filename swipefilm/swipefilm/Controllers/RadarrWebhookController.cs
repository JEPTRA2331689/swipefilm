// swipefilm/Controllers/RadarrWebhookController.cs
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using swipefilm.Auth;
using swipefilm.Data;
using swipefilm.Models;

namespace swipefilm.Controllers
{
    /// <summary>
    /// Reçoit les événements "Connect → Webhook" de Radarr (Grab/Download) pour
    /// mettre à jour le statut des requêtes en temps réel, sans attendre le
    /// job de polling de secours. Pas de [Authorize] — Radarr ne sait pas
    /// envoyer un Bearer JWT — la sécurité passe par le token en query string.
    /// </summary>
    [ApiController]
    [Route("api/webhooks/radarr")]
    public class RadarrWebhookController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IAppConfigService _config;
        private readonly ILogger<RadarrWebhookController> _logger;

        public RadarrWebhookController(
            AppDbContext db, IAppConfigService config, ILogger<RadarrWebhookController> logger)
        {
            _db = db;
            _config = config;
            _logger = logger;
        }

        // ✅ Sans ça, un GET (ex: coller l'URL dans un navigateur pour tester)
        // ne matche aucune méthode sur cette route et retombe sur le
        // MapFallback du frontend — on atterrit sur la page web au lieu d'un
        // message clair. N'affecte pas Radarr, qui n'envoie que du POST.
        [HttpGet]
        public IActionResult Ping() =>
            Ok(new { message = "Endpoint webhook Radarr — attend un POST de Radarr, pas une visite navigateur." });

        [HttpPost]
        public async Task<IActionResult> Receive(
            [FromQuery] string? token, [FromBody] JsonElement body)
        {
            var expected = _config.GetRadarr()?.WebhookToken;
            if (string.IsNullOrEmpty(expected) || token != expected)
                return Unauthorized();

            var eventType = body.TryGetProperty("eventType", out var et) ? et.GetString() : null;

            // ✅ Bouton "Test" de Radarr — répond OK sans rien faire d'autre
            if (eventType == "Test") return Ok();

            // ✅ "Grab" (release trouvée, téléchargement lancé) et "Download"
            // (fichier importé dans le dossier Radarr) ne veulent dire que ça —
            // pas que Jellyfin/Plex l'a scanné. Le passage à Available n'est
            // confirmé que par ConfirmAvailabilityAsync (sync Jellyfin/Plex).
            var newStatus = eventType switch
            {
                "Grab" or "Download" => RequestStatus.Downloading,
                _ => (RequestStatus?)null,
            };

            if (newStatus is null) return Ok();

            if (!body.TryGetProperty("movie", out var movieEl) ||
                !movieEl.TryGetProperty("tmdbId", out var tmdbProp))
                return Ok();

            var tmdbId = tmdbProp.GetInt32();

            var movie = await _db.Movies.FirstOrDefaultAsync(m => m.TmdbId == tmdbId);
            if (movie is null) return Ok();

            var requests = await _db.MediaRequests
                .Where(r => r.MovieId == movie.Id && r.Status == RequestStatus.Approved)
                .ToListAsync();

            if (!requests.Any()) return Ok();

            foreach (var req in requests)
                req.Status = newStatus.Value;

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "[Webhook Radarr] {Event} → {Count} requête(s) mises à jour pour tmdbId {TmdbId}",
                eventType, requests.Count, tmdbId);

            return Ok();
        }
    }
}
