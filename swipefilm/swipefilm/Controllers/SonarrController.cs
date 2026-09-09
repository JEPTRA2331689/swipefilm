// swipefilm/Controllers/SonarrController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using swipefilm.Auth;
using swipefilm.Models;

namespace swipefilm.Controllers
{
    [ApiController]
    [Route("api/sonarr")]
    [Authorize]
    public class SonarrController : ControllerBase
    {
        private readonly SonarrService _sonarr;
        private readonly IAppConfigService _config;

        public SonarrController(SonarrService sonarr, IAppConfigService config)
        {
            _sonarr = sonarr;
            _config = config;
        }

        // ─── Configuration ────────────────────────────────────────────

        [HttpPost("test")]
        [RequirePermission(Permission.Admin)]
        public async Task<IActionResult> Test([FromBody] ArrConfigDto dto)
        {
            var ok = await _sonarr.TestConnectionAsync(dto.Url, dto.ApiKey);
            return ok
                ? Ok(new { success = true, message = "Connexion Sonarr réussie" })
                : BadRequest(new { success = false, message = "Connexion Sonarr impossible" });
        }

        [HttpPut]
        [RequirePermission(Permission.Admin)]
        public async Task<IActionResult> Configure([FromBody] ArrConfigDto dto)
        {
            var ok = await _sonarr.TestConnectionAsync(dto.Url, dto.ApiKey);
            if (!ok) return BadRequest(new { error = "Connexion Sonarr impossible" });

            await _config.SetSonarrAsync(dto.Url.TrimEnd('/'), dto.ApiKey);

            var publicUrl = _config.GetPublicUrl();
            bool webhookRegistered = false;
            string? callbackUrl = null;
            string? webhookError = "Adresse publique non configurée — le webhook n'a pas été enregistré automatiquement";

            if (publicUrl is not null)
                (webhookRegistered, callbackUrl, webhookError) = await _sonarr.RegisterWebhookAsync(publicUrl);

            return Ok(new
            {
                message = "Sonarr configuré",
                testConnectionOk = true,
                webhookRegistered,
                callbackUrl,
                webhookError,
            });
        }

        [HttpDelete]
        [RequirePermission(Permission.Admin)]
        public async Task<IActionResult> Remove()
        {
            if (_config.GetSonarr() is null) return NotFound();
            await _config.RemoveSonarrAsync();
            return Ok(new { message = "Sonarr déconnecté" });
        }

        /// <summary>Vérifie et (ré)enregistre le webhook Sonarr → SwipeFilm à la demande.</summary>
        [HttpPost("webhook/verify")]
        [RequirePermission(Permission.Admin)]
        public async Task<IActionResult> VerifyWebhook()
        {
            if (_config.GetSonarr() is null)
                return NotFound(new { error = "Sonarr non configuré" });

            var publicUrl = _config.GetPublicUrl();
            if (publicUrl is null)
                return BadRequest(new
                {
                    success = false,
                    callbackUrl = "",
                    error = "Adresse publique non configurée — renseigne-la dans Paramètres avant de vérifier le webhook",
                });

            var (success, callbackUrl, error) = await _sonarr.RegisterWebhookAsync(publicUrl);

            return Ok(new { success, callbackUrl, error });
        }

        [HttpGet("status")]
        [RequirePermission(Permission.Admin)]
        public IActionResult GetStatus()
        {
            return Ok(new { isConfigured = _config.GetSonarr() is not null });
        }

        [HttpGet]
        [RequirePermission(Permission.Admin)]
        public IActionResult GetConfig()
        {
            var sonarr = _config.GetSonarr();
            if (sonarr is null)
                return NotFound(new { error = "Sonarr non configuré" });

            var apiKey = sonarr.ApiKey;

            return Ok(new
            {
                Url = sonarr.Url,
                ApiKeyHint = apiKey.Length > 4
                    ? $"{apiKey[..4]}{"*".PadRight(apiKey.Length - 4, '*')}"
                    : "****",
                IsConfigured = true,
                DefaultQualityProfileId = sonarr.DefaultQualityProfileId,
                DefaultRootFolderPath = sonarr.DefaultRootFolderPath,
            });
        }

        // ─── Actions séries (lecture — accessible à tout utilisateur avec CanRequest) ─

        [HttpGet("series/{tvdbId}")]
        [RequirePermission(Permission.CanRequest)]
        public async Task<IActionResult> GetSeriesStatus(int tvdbId)
        {
            var status = await _sonarr.GetSeriesStatusAsync(tvdbId);
            return Ok(status);
        }

        [HttpPost("series/bulk-status")]
        [RequirePermission(Permission.CanRequest)]
        public async Task<IActionResult> GetBulkStatus([FromBody] BulkStatusDto dto)
        {
            var statuses = await _sonarr.GetBulkStatusAsync(dto.TmdbIds);
            return Ok(statuses);
        }

        /// <summary>Ajouter une série à Sonarr directement (hors flux de demande — admin uniquement)</summary>
        [HttpPost("series/{tvdbId}")]
        [RequirePermission(Permission.ManageRequests)]
        public async Task<IActionResult> AddSeries(int tvdbId, [FromBody] AddMovieDto dto)
        {
            var (success, message) = await _sonarr.AddSeriesAsync(
                tvdbId, dto.Title, dto.Year);

            return success
                ? Ok(new { message })
                : BadRequest(new { error = message });
        }

        [HttpDelete("series/{tvdbId}")]
        [RequirePermission(Permission.ManageRequests)]
        public async Task<IActionResult> RemoveSeries(
            int tvdbId, [FromQuery] bool deleteFiles = false)
        {
            var (success, message) = await _sonarr.RemoveSeriesAsync(
                tvdbId, deleteFiles);

            return success
                ? Ok(new { message })
                : BadRequest(new { error = message });
        }

        /// <summary>Accessible aux demandeurs pour choisir une qualité à la
        /// demande — ne renvoie ni URL ni clé API.</summary>
        [HttpGet("quality-profiles")]
        [RequirePermission(Permission.CanRequest)]
        public async Task<IActionResult> GetQualityProfiles()
        {
            var profiles = await _sonarr.GetQualityProfilesAsync();
            if (profiles is null)
                return NotFound(new { error = "Sonarr non configuré ou injoignable" });

            return Ok(profiles.Select(p => new { Id = p.Id, Name = p.Name }));
        }

        [HttpPut("preferences")]
        [RequirePermission(Permission.Admin)]
        public async Task<IActionResult> SetPreferences([FromBody] ArrPreferencesDto dto)
        {
            if (_config.GetSonarr() is null)
                return NotFound(new { error = "Sonarr non configuré" });

            await _config.SetSonarrPreferencesAsync(
                dto.QualityProfileId, dto.QualityProfileName, dto.RootFolderPath);

            return Ok(new { message = "Préférences Sonarr sauvegardées" });
        }

        /// <summary>Accessible aux demandeurs — même raisonnement que quality-profiles.</summary>
        [HttpGet("root-folders")]
        [RequirePermission(Permission.CanRequest)]
        public async Task<IActionResult> GetRootFolders()
        {
            var folders = await _sonarr.GetRootFoldersAsync();
            if (folders is null)
                return NotFound(new { error = "Sonarr non configuré ou injoignable" });

            return Ok(folders.Select(f => new
            {
                Id = f.Id,
                Path = f.Path,
                FreeSpace = f.FreeSpace,
                TotalSpace = f.TotalSpace
            }));
        }
    }
}
