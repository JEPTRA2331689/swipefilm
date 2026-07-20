// swipefilm/Controllers/RadarrController.cs
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using swipefilm.Auth;
using swipefilm.Models;

namespace swipefilm.Controllers
{
    [ApiController]
    [Route("api/radarr")]
    [Authorize]
    public class RadarrController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly RadarrService _radarr;
        private readonly IEncryptionService _encryption;
        private readonly IHttpClientFactory _httpFactory; // ✅ ajout


        public RadarrController(
            AppDbContext db,
            RadarrService radarr,
            IEncryptionService encryption,
            IHttpClientFactory httpFactory)
        {
            _db = db;
            _radarr = radarr;
            _encryption = encryption;
            _httpFactory = httpFactory;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

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
            // Valide d'abord
            var ok = await _radarr.TestConnectionAsync(dto.Url, dto.ApiKey);
            if (!ok)
                return BadRequest(new { error = "Connexion Radarr impossible" });

            var existing = await _db.UserRadarr
                .FirstOrDefaultAsync(r => r.UserId == CurrentUserId);

            if (existing is null)
            {
                _db.UserRadarr.Add(new UserRadarr
                {
                    Id = Guid.NewGuid(),
                    UserId = CurrentUserId,
                    UrlEncrypted = _encryption.Encrypt(dto.Url.TrimEnd('/')),
                    ApiKeyEncrypted = _encryption.Encrypt(dto.ApiKey),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.UrlEncrypted = _encryption.Encrypt(dto.Url.TrimEnd('/'));
                existing.ApiKeyEncrypted = _encryption.Encrypt(dto.ApiKey);
                existing.IsActive = true;
            }

            await _db.SaveChangesAsync();
            return Ok(new { message = "Radarr configuré" });
        }

        /// <summary>Déconnecter Radarr</summary>
        [HttpDelete]
        [RequirePermission(Permission.Admin)]
        public async Task<IActionResult> Remove()
        {
            var existing = await _db.UserRadarr
                .FirstOrDefaultAsync(r => r.UserId == CurrentUserId);

            if (existing is null) return NotFound();

            existing.IsActive = false;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Radarr déconnecté" });
        }

        /// <summary>Statut de la configuration</summary>
        [HttpGet("status")]
        [RequirePermission(Permission.Admin)]
        public async Task<IActionResult> GetStatus()
        {
            var existing = await _db.UserRadarr
                .FirstOrDefaultAsync(r => r.IsActive);

            return Ok(new { isConfigured = existing is not null });
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
        public async Task<IActionResult> GetConfig()
        {
            var existing = await _db.UserRadarr
                .FirstOrDefaultAsync(r => r.IsActive);

            if (existing is null)
                return NotFound(new { error = "Radarr non configuré" });

            // ✅ Retourne l'URL déchiffrée mais PAS l'API key complète — sécurité
            var url = _encryption.Decrypt(existing.UrlEncrypted);
            var apiKey = _encryption.Decrypt(existing.ApiKeyEncrypted);

            return Ok(new
            {
                existing.Id,
                Url = url,
                ApiKeyHint = $"{apiKey[..4]}{"*".PadRight(apiKey.Length - 4, '*')}", // ex: "abc1****"
                existing.IsActive,
                existing.CreatedAt
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
            var existing = await _db.UserRadarr
                .FirstOrDefaultAsync(r => r.IsActive);
            if (existing is null)
                return NotFound(new { error = "Radarr non configuré" });

            var url = _encryption.Decrypt(existing.UrlEncrypted);
            var apiKey = _encryption.Decrypt(existing.ApiKeyEncrypted);

            try
            {
                var http = _httpFactory.CreateClient();
                var response = await http.GetAsync(
                    $"{url}/api/v3/qualityprofile?apikey={apiKey}");

                if (!response.IsSuccessStatusCode)
                    return BadRequest(new { error = "Impossible de récupérer les profils" });

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                var profiles = json.EnumerateArray().Select(p => new
                {
                    Id = p.GetProperty("id").GetInt32(),
                    Name = p.GetProperty("name").GetString()
                }).ToList();

                return Ok(profiles);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>Sauvegarder le profil et dossier par défaut</summary>
        [HttpPut("preferences")]
        [RequirePermission(Permission.Admin)]
        public async Task<IActionResult> SetPreferences([FromBody] ArrPreferencesDto dto)
        {
            var existing = await _db.UserRadarr
                .FirstOrDefaultAsync(r => r.IsActive);

            if (existing is null)
                return NotFound(new { error = "Radarr non configuré" });

            existing.DefaultQualityProfileId = dto.QualityProfileId;
            existing.DefaultQualityProfileName = dto.QualityProfileName;
            existing.DefaultRootFolderPath = dto.RootFolderPath;

            await _db.SaveChangesAsync();
            return Ok(new { message = "Préférences Radarr sauvegardées" });
        }

        /// <summary>Liste les dossiers racines disponibles sur Radarr — même
        /// raisonnement que quality-profiles, accessible aux demandeurs.</summary>
        [HttpGet("root-folders")]
        [RequirePermission(Permission.CanRequest)]
        public async Task<IActionResult> GetRootFolders()
        {
            var existing = await _db.UserRadarr
                .FirstOrDefaultAsync(r => r.IsActive);
            if (existing is null)
                return NotFound(new { error = "Radarr non configuré" });

            var url = _encryption.Decrypt(existing.UrlEncrypted);
            var apiKey = _encryption.Decrypt(existing.ApiKeyEncrypted);

            try
            {
                var http = _httpFactory.CreateClient();
                var response = await http.GetAsync(
                    $"{url}/api/v3/rootfolder?apikey={apiKey}");

                if (!response.IsSuccessStatusCode)
                    return BadRequest(new { error = "Impossible de récupérer les dossiers" });

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                var folders = json.EnumerateArray().Select(f => new
                {
                    Id = f.GetProperty("id").GetInt32(),
                    Path = f.GetProperty("path").GetString(),
                    FreeSpace = f.TryGetProperty("freeSpace", out var fs) ? fs.GetInt64() : 0,
                    TotalSpace = f.TryGetProperty("totalSpace", out var ts) ? ts.GetInt64() : 0
                }).ToList();

                return Ok(folders);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
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