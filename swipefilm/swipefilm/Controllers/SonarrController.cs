// swipefilm/Controllers/SonarrController.cs
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
    [Route("api/sonarr")]
    [Authorize]
    public class SonarrController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly SonarrService _sonarr;
        private readonly IEncryptionService _encryption;
        private readonly IHttpClientFactory _httpFactory; // ✅ ajout


        public SonarrController(
            AppDbContext db,
            SonarrService sonarr,
            IEncryptionService encryption,
            IHttpClientFactory httpFactory)
        {
            _db = db;
            _sonarr = sonarr;
            _encryption = encryption;
            _httpFactory = httpFactory;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

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

            var existing = await _db.UserSonarr
                .FirstOrDefaultAsync(s => s.UserId == CurrentUserId);

            if (existing is null)
            {
                _db.UserSonarr.Add(new UserSonarr
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
            return Ok(new { message = "Sonarr configuré" });
        }

        [HttpDelete]
        [RequirePermission(Permission.Admin)]
        public async Task<IActionResult> Remove()
        {
            var existing = await _db.UserSonarr
                .FirstOrDefaultAsync(s => s.UserId == CurrentUserId);

            if (existing is null) return NotFound();

            existing.IsActive = false;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Sonarr déconnecté" });
        }

        [HttpGet("status")]
        [RequirePermission(Permission.Admin)]
        public async Task<IActionResult> GetStatus()
        {
            var existing = await _db.UserSonarr
                .FirstOrDefaultAsync(s => s.IsActive);

            return Ok(new { isConfigured = existing is not null });
        }

        [HttpGet]
        [RequirePermission(Permission.Admin)]
        public async Task<IActionResult> GetConfig()
        {
            var existing = await _db.UserSonarr
                .FirstOrDefaultAsync(s => s.IsActive);

            if (existing is null)
                return NotFound(new { error = "Sonarr non configuré" });

            var url = _encryption.Decrypt(existing.UrlEncrypted);
            var apiKey = _encryption.Decrypt(existing.ApiKeyEncrypted);

            return Ok(new
            {
                existing.Id,
                Url = url,
                ApiKeyHint = $"{apiKey[..4]}{"*".PadRight(apiKey.Length - 4, '*')}",
                existing.IsActive,
                existing.CreatedAt
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
            var existing = await _db.UserSonarr
                .FirstOrDefaultAsync(s => s.IsActive);
            if (existing is null)
                return NotFound(new { error = "Sonarr non configuré" });

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
        [HttpPut("preferences")]
        [RequirePermission(Permission.Admin)]
        public async Task<IActionResult> SetPreferences([FromBody] ArrPreferencesDto dto)
        {
            var existing = await _db.UserSonarr
                .FirstOrDefaultAsync(s => s.IsActive);

            if (existing is null)
                return NotFound(new { error = "Sonarr non configuré" });

            existing.DefaultQualityProfileId = dto.QualityProfileId;
            existing.DefaultQualityProfileName = dto.QualityProfileName;
            existing.DefaultRootFolderPath = dto.RootFolderPath;

            await _db.SaveChangesAsync();
            return Ok(new { message = "Préférences Sonarr sauvegardées" });
        }

        /// <summary>Accessible aux demandeurs — même raisonnement que quality-profiles.</summary>
        [HttpGet("root-folders")]
        [RequirePermission(Permission.CanRequest)]
        public async Task<IActionResult> GetRootFolders()
        {
            var existing = await _db.UserSonarr
                .FirstOrDefaultAsync(s => s.IsActive);
            if (existing is null)
                return NotFound(new { error = "Sonarr non configuré" });

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
    }
}