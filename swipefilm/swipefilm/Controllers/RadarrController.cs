// swipefilm/Controllers/RadarrController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using swipefilm.Auth;
using swipefilm.Models;

namespace swipefilm.Controllers
{
    [ApiController]
    [Route("api/radarr")]
    [Authorize]
    public class RadarrController : ControllerBase
    {
        private readonly RadarrService _radarr;
        private readonly IAppConfigService _config;

        public RadarrController(RadarrService radarr, IAppConfigService config)
        {
            _radarr = radarr;
            _config = config;
        }

        // ─── Configuration (réservée admin — config globale d'instance) ─

        /// <summary>Tester la connexion sans sauvegarder</summary>
        [HttpPost("test")]
        [RequirePermission(Permission.Admin)]
        public async Task<IActionResult> Test([FromBody] ArrConfigDto dto)
        {
            var ok = await _radarr.TestConnectionAsync(dto.Url, dto.ApiKey);
            return ok
                ? Ok(new { success = true, message = "Connexion Radarr réussie" })
                : BadRequest(new { success = false, message = "Connexion Radarr impossible — vérifie l'URL et l'API key" });
        }

        /// <summary>Configurer ou mettre à jour Radarr</summary>
        [HttpPut]
        [RequirePermission(Permission.Admin)]
        public async Task<IActionResult> Configure([FromBody] ArrConfigDto dto)
        {
            var ok = await _radarr.TestConnectionAsync(dto.Url, dto.ApiKey);
            if (!ok)
                return BadRequest(new { error = "Connexion Radarr impossible" });

            await _config.SetRadarrAsync(dto.Url.TrimEnd('/'), dto.ApiKey);

            var publicUrl = _config.GetPublicUrl();
            bool webhookRegistered = false;
            string? callbackUrl = null;
            string? webhookError = "Adresse publique non configurée — le webhook n'a pas été enregistré automatiquement";

            if (publicUrl is not null)
                (webhookRegistered, callbackUrl, webhookError) = await _radarr.RegisterWebhookAsync(publicUrl);

            return Ok(new
            {
                message = "Radarr configuré",
                testConnectionOk = true,
                webhookRegistered,
                callbackUrl,
                webhookError,
            });
        }

        /// <summary>Déconnecter Radarr</summary>
        [HttpDelete]
        [RequirePermission(Permission.Admin)]
        public async Task<IActionResult> Remove()
        {
            if (_config.GetRadarr() is null) return NotFound();
            await _config.RemoveRadarrAsync();
            return Ok(new { message = "Radarr déconnecté" });
        }

        /// <summary>Vérifie et (ré)enregistre le webhook Radarr → SwipeFilm à la demande.</summary>
        [HttpPost("webhook/verify")]
        [RequirePermission(Permission.Admin)]
        public async Task<IActionResult> VerifyWebhook()
        {
            if (_config.GetRadarr() is null)
                return NotFound(new { error = "Radarr non configuré" });

            var publicUrl = _config.GetPublicUrl();
            if (publicUrl is null)
                return BadRequest(new
                {
                    success = false,
                    callbackUrl = "",
                    error = "Adresse publique non configurée — renseigne-la dans Paramètres avant de vérifier le webhook",
                });

            var (success, callbackUrl, error) = await _radarr.RegisterWebhookAsync(publicUrl);

            return Ok(new { success, callbackUrl, error });
        }

        /// <summary>Statut de la configuration</summary>
        [HttpGet("status")]
        [RequirePermission(Permission.Admin)]
        public IActionResult GetStatus()
        {
            return Ok(new { isConfigured = _config.GetRadarr() is not null });
        }

        // ─── Actions films (lecture — accessible à tout utilisateur avec CanRequest) ─

        /// <summary>Statut d'un film dans Radarr</summary>
        [HttpGet("movie/{tmdbId}")]
        [RequirePermission(Permission.CanRequest)]
        public async Task<IActionResult> GetMovieStatus(int tmdbId)
        {
            var status = await _radarr.GetMovieStatusAsync(tmdbId);
            return Ok(status);
        }

        /// <summary>Statuts de plusieurs films en une seule requête</summary>
        [HttpPost("movies/bulk-status")]
        [RequirePermission(Permission.CanRequest)]
        public async Task<IActionResult> GetBulkStatus([FromBody] BulkStatusDto dto)
        {
            var statuses = await _radarr.GetBulkStatusAsync(dto.TmdbIds);
            return Ok(statuses);
        }

        [HttpGet]
        [RequirePermission(Permission.Admin)]
        public IActionResult GetConfig()
        {
            var radarr = _config.GetRadarr();
            if (radarr is null)
                return NotFound(new { error = "Radarr non configuré" });

            // ✅ Retourne l'URL mais PAS l'API key complète — sécurité
            var apiKey = radarr.ApiKey;

            return Ok(new
            {
                Url = radarr.Url,
                ApiKeyHint = apiKey.Length > 4
                    ? $"{apiKey[..4]}{"*".PadRight(apiKey.Length - 4, '*')}"
                    : "****",
                IsConfigured = true,
                DefaultQualityProfileId = radarr.DefaultQualityProfileId,
                DefaultRootFolderPath = radarr.DefaultRootFolderPath,
            });
        }

        /// <summary>
        /// Liste les profils qualité disponibles sur Radarr — accessible aux
        /// demandeurs (pas seulement l'admin) pour choisir une qualité au
        /// moment de la demande ; ne renvoie ni URL ni clé API.
        /// </summary>
        [HttpGet("quality-profiles")]
        [RequirePermission(Permission.CanRequest)]
        public async Task<IActionResult> GetQualityProfiles()
        {
            var profiles = await _radarr.GetQualityProfilesAsync();
            if (profiles is null)
                return NotFound(new { error = "Radarr non configuré ou injoignable" });

            return Ok(profiles.Select(p => new { Id = p.Id, Name = p.Name }));
        }

        /// <summary>Sauvegarder le profil et dossier par défaut</summary>
        [HttpPut("preferences")]
        [RequirePermission(Permission.Admin)]
        public async Task<IActionResult> SetPreferences([FromBody] ArrPreferencesDto dto)
        {
            if (_config.GetRadarr() is null)
                return NotFound(new { error = "Radarr non configuré" });

            await _config.SetRadarrPreferencesAsync(
                dto.QualityProfileId, dto.QualityProfileName, dto.RootFolderPath);

            return Ok(new { message = "Préférences Radarr sauvegardées" });
        }

        /// <summary>Liste les dossiers racines disponibles sur Radarr — même
        /// raisonnement que quality-profiles, accessible aux demandeurs.</summary>
        [HttpGet("root-folders")]
        [RequirePermission(Permission.CanRequest)]
        public async Task<IActionResult> GetRootFolders()
        {
            var folders = await _radarr.GetRootFoldersAsync();
            if (folders is null)
                return NotFound(new { error = "Radarr non configuré ou injoignable" });

            return Ok(folders.Select(f => new
            {
                Id = f.Id,
                Path = f.Path,
                FreeSpace = f.FreeSpace,
                TotalSpace = f.TotalSpace
            }));
        }

        /// <summary>Ajouter un film à Radarr directement (hors flux de demande — admin uniquement)</summary>
        [HttpPost("movie/{tmdbId}")]
        [RequirePermission(Permission.ManageRequests)]
        public async Task<IActionResult> AddMovie(int tmdbId, [FromBody] AddMovieDto dto)
        {
            var (success, message) = await _radarr.AddMovieAsync(
                tmdbId, dto.Title, dto.Year);

            return success
                ? Ok(new { message })
                : BadRequest(new { error = message });
        }

        /// <summary>Retirer un film de Radarr</summary>
        [HttpDelete("movie/{tmdbId}")]
        [RequirePermission(Permission.ManageRequests)]
        public async Task<IActionResult> RemoveMovie(
            int tmdbId, [FromQuery] bool deleteFiles = false)
        {
            var (success, message) = await _radarr.RemoveMovieAsync(
                tmdbId, deleteFiles);

            return success
                ? Ok(new { message })
                : BadRequest(new { error = message });
        }
    }

    public record ArrConfigDto(string Url, string ApiKey);
    public record AddMovieDto(string Title, int Year);
    public record BulkStatusDto(List<int> TmdbIds);
    public record ArrPreferencesDto(
        int QualityProfileId,
        string QualityProfileName,
        string RootFolderPath
    );
}
