// swipefilm/Controllers/SetupController.cs
using System.Text.Json;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using swipefilm.Auth;
using swipefilm.Models;

namespace swipefilm.Controllers
{
    [ApiController]
    [Route("api/setup")]
    public class SetupController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly UserManager<User> _userManager;
        private readonly AuthManager _authManager;
        private readonly IServerConfigService _serverService;
        private readonly IEncryptionService _encryption;

        public SetupController(
            AppDbContext db,
            UserManager<User> userManager,
            AuthManager authManager,
            IServerConfigService serverService,
            IEncryptionService encryption)
        {
            _db = db;
            _userManager = userManager;
            _authManager = authManager;
            _serverService = serverService;
            _encryption = encryption;
        }

        // ─── Vérifie si le setup est nécessaire ──────────────────────
        // Appelé au démarrage de l'app mobile pour savoir où rediriger

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            var setting = await _db.AppSettings.FindAsync("IsSetupComplete");
            var isComplete = setting?.Value == "true";
            return Ok(new { IsSetupComplete = isComplete });
        }

        // ─── Étape 1 : compte admin ───────────────────────────────────

        [HttpPost("admin")]
        public async Task<IActionResult> CreateAdmin([FromBody] CreateAdminDto dto)
        {
            // Bloque si déjà configuré
            var setting = await _db.AppSettings.FindAsync("IsSetupComplete");
            if (setting?.Value == "true")
                return BadRequest(new { Error = "L'application est déjà configurée" });

            // Vérifie qu'il n'y a pas déjà un admin
            var existingAdmin = await _userManager.GetUsersInRoleAsync("Admin");
            if (existingAdmin.Any())
                return BadRequest(new { Error = "Un administrateur existe déjà" });

            var user = new User
            {
                Email = dto.Email,
                UserName = dto.Email,
                DisplayName = dto.DisplayName,
                CreatedAt = DateTime.UtcNow,
                // ✅ Sans ça, le compte reste sur DefaultUser — bloqué par tous
                // les [RequirePermission(Permission.Admin)] du reste du setup
                // (test Radarr/Sonarr, /me, etc.)
                Permissions = (long)Permission.FullAdmin,
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            await _userManager.AddToRoleAsync(user, "Admin");

            return Ok(new
            {
                UserId = user.Id,
                Token = _authManager.GenerateToken(user),
                Message = "Compte admin créé — passez à l'étape serveur"
            });
        }

        // ─── Étape 2 : connexion serveur + import users ───────────────

        [HttpPost("server")]
        public async Task<IActionResult> ConfigureServer([FromBody] SetupServerDto dto)
        {
            Console.WriteLine(dto);
            var setting = await _db.AppSettings.FindAsync("IsSetupComplete");
            if (setting?.Value == "true")
                return BadRequest(new { Error = "L'application est déjà configurée" });

            // Récupère l'admin
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            var admin = admins.FirstOrDefault();
            if (admin is null)
                return BadRequest(new { Error = "Créez d'abord le compte admin (étape 1)" });

            // ✅ Configure l'unique serveur de l'instance
            ServerConfig server;
            try
            {
                server = await _serverService.ConfigureAsync(new AddServerDto(
                    FriendlyName: dto.FriendlyName,
                    Type: dto.Type,
                    ApiKey: dto.ApiKey,
                    Username: dto.Username,
                    Password: dto.Password,
                    Url: dto.Url
                ));
            }
            catch (Exception ex)
            {
                return BadRequest(new { Error = $"Connexion impossible : {ex.Message}" });
            }

            // ✅ Rechiffre à partir de ce que ConfigureAsync a réellement résolu et
            // sauvegardé — dto.ApiKey peut être null (flux username/password), et
            // dto.Url peut être vide pour Plex (résolu dynamiquement à l'auth).
            var resolvedUrl = _encryption.Decrypt(server.UrlEncrypted);
            var resolvedToken = _encryption.Decrypt(server.TokenEncrypted);

            // Importe et synchronise les utilisateurs depuis Jellyfin/Plex
            var (imported, adminsImported) = await ImportUsersFromServerAsync(
                server, dto.Type, resolvedUrl, resolvedToken);

            // Marque le setup comme terminé
            if (setting is null)
                _db.AppSettings.Add(new AppSettings
                {
                    Key = "IsSetupComplete",
                    Value = "true"
                });
            else
                setting.Value = "true";

            var defaultPermSetting = await _db.AppSettings.FindAsync("DefaultPermissions");
            if (defaultPermSetting is null)
            {
                _db.AppSettings.Add(new AppSettings
                {
                    Key = "DefaultPermissions",
                    Value = ((long)Permission.DefaultUser).ToString() // 4128
                });
                await _db.SaveChangesAsync();
            }

            return Ok(new
            {
                Message = "Serveur configuré",
                UsersImported = imported,
                AdminsImported = adminsImported,
                NextStep = "seerr" // ← indique à l'app d'afficher l'étape Seerr
            });
        }

        // ─── Étape 3 : Seerr (optionnel) ─────────────────────────────

        [HttpPost("seerr")]
        public async Task<IActionResult> ConfigureSeerr([FromBody] SetupSeerrDto dto)
        {
            var setting = await _db.AppSettings.FindAsync("IsSetupComplete");
            if (setting?.Value != "true")
                return BadRequest(new { Error = "Configurez d'abord le serveur (étape 2)" });

            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            var admin = admins.FirstOrDefault();
            if (admin is null) return BadRequest();

            // Vérifie que Seerr est accessible
            try
            {
                using var http = new HttpClient();
                var response = await http.GetAsync(
                    $"{dto.Url.TrimEnd('/')}/api/v1/status?apikey={dto.ApiKey}");

                if (!response.IsSuccessStatusCode)
                    return BadRequest(new { Error = "Connexion Seerr impossible — vérifie l'URL et l'API key" });
            }
            catch
            {
                return BadRequest(new { Error = "Seerr inaccessible à cette URL" });
            }

            // ✅ Chiffre l'URL et l'API key comme UserServer le fait
            var encryptedUrl = _encryption.Encrypt(dto.Url.TrimEnd('/'));
            var encryptedApiKey = _encryption.Encrypt(dto.ApiKey);

            var existing = await _db.UserSeerr
                .FirstOrDefaultAsync(s => s.UserId == admin.Id);

            if (existing is null)
            {
                _db.UserSeerr.Add(new UserSeerr
                {
                    Id = Guid.NewGuid(),
                    UserId = admin.Id,
                    UrlEncrypted = encryptedUrl,
                    ApiKeyEncrypted = encryptedApiKey,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.UrlEncrypted = encryptedUrl;
                existing.ApiKeyEncrypted = encryptedApiKey;
            }

            await _db.SaveChangesAsync();

            return Ok(new { Message = "Seerr configuré — setup terminé !" });
        }

        // ─── Skip Seerr ───────────────────────────────────────────────

        [HttpPost("seerr/skip")]
        public IActionResult SkipSeerr()
        {
            return Ok(new { Message = "Setup terminé sans Seerr" });
        }

        // ─── Import users depuis Jellyfin/Plex ───────────────────────

        private async Task<(int imported, int admins)> ImportUsersFromServerAsync(
            ServerConfig server, ServerType type, string url, string apiKey)
        {
            int imported = 0;
            int adminCount = 0;

            // ✅ Chaque utilisateur nouvellement importé (pas ceux qui existaient
            // déjà) doit avoir sync + profil + discover lancés en tâche de fond,
            // pour avoir des recommandations dès sa première visite.
            var newlyImported = new List<Guid>();

            // ✅ Récupère les permissions par défaut depuis AppSettings
            var defaultSetting = await _db.AppSettings.FindAsync("DefaultPermissions");
            var defaultPerms = long.TryParse(defaultSetting?.Value, out var dp)
                ? dp
                : (long)Permission.DefaultUser;

            if (type == ServerType.Jellyfin)
            {
                var users = await GetJellyfinUsersAsync(url, apiKey);

                foreach (var u in users)
                {
                    // ✅ Identifiant primaire = JellyfinUserId
                    var existingByJellyfinId = await _db.Users
                        .FirstOrDefaultAsync(x => x.JellyfinUserId == u.ServerId);

                    if (existingByJellyfinId is not null)
                    {
                        // ✅ Met à jour les permissions si le statut admin a changé
                        var shouldBeAdmin = u.IsAdmin;
                        var isCurrentlyAdmin = existingByJellyfinId.IsAdmin;

                        if (shouldBeAdmin != isCurrentlyAdmin)
                        {
                            existingByJellyfinId.Permissions = shouldBeAdmin
                                ? (long)Permission.FullAdmin
                                : defaultPerms;
                        }
                        continue;
                    }

                    // ✅ Gère les doublons d'email
                    var email = $"{u.Name.ToLower().Trim()}@jellyfin.local";
                    var emailConflict = await _userManager.FindByEmailAsync(email);
                    var (serverLocale, serverRegion) = await GetJellyfinServerLocaleAsync(url, apiKey);

                    if (emailConflict is not null)
                        email = $"{u.Name.ToLower().Trim()}.{u.ServerId[..8]}@jellyfin.local";

                    var newUser = new User
                    {
                        Email = email,
                        UserName = email,
                        DisplayName = u.Name,
                        CreatedAt = DateTime.UtcNow,
                        JellyfinUserId = u.ServerId,
                        // ✅ Admin Jellyfin → FullAdmin, sinon permissions par défaut
                        Permissions = u.IsAdmin
                            ? (long)Permission.FullAdmin
                            : defaultPerms,
                        Locale = serverLocale,   // ex: "fr" depuis UICulture "fr-FR"
                        Region = serverRegion,   // ex: "FR" depuis MetadataCountryCode
                        OriginalLanguage = serverLocale,   // même valeur — langue préférée = locale serveur
                    };

                    var tempPassword = $"{Guid.NewGuid():N}{Guid.NewGuid():N}"[..16] + "Aa1!";
                    var result = await _userManager.CreateAsync(newUser, tempPassword);
                    if (!result.Succeeded)
                    {
                        Console.WriteLine($"[Setup] Échec import {u.Name}: " +
                            string.Join(", ", result.Errors.Select(e => e.Description)));
                        continue;
                    }

                    // ✅ Rôle Identity gardé pour la compatibilité ASP.NET
                    // mais la vraie autorisation passe par Permissions (bitmask)
                    await _userManager.AddToRoleAsync(newUser, u.IsAdmin ? "Admin" : "User");
                    if (u.IsAdmin) adminCount++;

                    // ✅ Plus besoin de créer une ligne serveur par utilisateur —
                    // il y en a un seul, partagé (newUser.JellyfinUserId suffit).
                    newlyImported.Add(newUser.Id);
                    imported++;
                }

                await _db.SaveChangesAsync();
            }
            else
            {
                var users = await GetPlexUsersAsync(url, apiKey);

                foreach (var u in users)
                {
                    // ✅ Plex — déduplique par PlexUserId si disponible, sinon par email
                    var existing = string.IsNullOrEmpty(u.ServerId)
                        ? await _userManager.FindByEmailAsync(u.Email)
                        : await _db.Users.FirstOrDefaultAsync(
                            x => x.PlexUserId == u.ServerId);

                    if (existing is not null)
                    {
                        // ✅ Met à jour les permissions si changement
                        var shouldBeAdmin = u.IsAdmin;
                        if (existing.IsAdmin != shouldBeAdmin)
                        {
                            existing.Permissions = shouldBeAdmin
                                ? (long)Permission.FullAdmin
                                : defaultPerms;
                        }
                        continue;
                    }

                    var newUser = new User
                    {
                        Email = u.Email,
                        UserName = u.Email,
                        DisplayName = u.Name,
                        CreatedAt = DateTime.UtcNow,
                        PlexUserId = u.ServerId,
                        // ✅ Même logique que Jellyfin
                        Permissions = u.IsAdmin
                            ? (long)Permission.FullAdmin
                            : defaultPerms
                    };

                    var tempPassword = Guid.NewGuid().ToString("N")[..12] + "Aa1!";
                    var result = await _userManager.CreateAsync(newUser, tempPassword);
                    if (!result.Succeeded)
                    {
                        Console.WriteLine($"[Setup] Échec import Plex {u.Name}: " +
                            string.Join(", ", result.Errors.Select(e => e.Description)));
                        continue;
                    }

                    await _userManager.AddToRoleAsync(newUser, u.IsAdmin ? "Admin" : "User");
                    if (u.IsAdmin) adminCount++;

                    // ✅ Plus besoin de créer une ligne serveur par utilisateur —
                    // il y en a un seul, partagé (newUser.PlexUserId suffit).
                    newlyImported.Add(newUser.Id);
                    imported++;
                }

                await _db.SaveChangesAsync();
            }

            // ✅ Sync + profil + discover en tâche de fond pour chaque nouvel
            // utilisateur — après SaveChangesAsync pour que les lignes existent
            // déjà en base quand le job Hangfire s'exécute.
            foreach (var userId in newlyImported)
            {
                BackgroundJob.Enqueue<SyncBackgroundJobService>(
                    "default",
                    x => x.OnboardNewUserAsync(userId));
            }

            return (imported, adminCount);
        }

        private async Task<(string locale, string region)> GetJellyfinServerLocaleAsync(
    string url, string apiKey)
        {
            try
            {
                using var http = new HttpClient();
                var response = await http.GetAsync(
                    $"{url}/System/Configuration?api_key={apiKey}");

                if (!response.IsSuccessStatusCode)
                    return ("en", "US"); // ✅ fallback sécurisé

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();

                // ✅ UICulture retourne "fr-FR" ou "en-US" — on veut juste "fr" ou "en"
                var uiCulture = json.TryGetProperty("UICulture", out var ui)
                    ? ui.GetString() ?? "en-US"
                    : "en-US";

                // ✅ MetadataCountryCode retourne "FR", "US", "CA" directement
                var region = json.TryGetProperty("MetadataCountryCode", out var rc)
                    ? rc.GetString() ?? "US"
                    : "US";

                // ✅ Extrait "fr" depuis "fr-FR"
                var locale = uiCulture.Contains('-')
                    ? uiCulture.Split('-')[0].ToLower()
                    : uiCulture.ToLower();

                return (locale, region.ToUpper());
            }
            catch
            {
                return ("en", "US"); // ✅ fallback si le serveur ne répond pas
            }
        }

        private async Task<List<ServerUser>> GetJellyfinUsersAsync(string url, string apiKey)
        {
            using var http = new HttpClient();
            var response = await http.GetAsync($"{url}/Users?api_key={apiKey}");
            if (!response.IsSuccessStatusCode) return [];

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            var users = new List<ServerUser>();

            foreach (var u in json.EnumerateArray())
            {
                var jellyfinId = u.GetProperty("Id").GetString() ?? "";
                var name = u.GetProperty("Name").GetString() ?? "";

                // ✅ IsAdministrator est dans Policy — confirmé sur tes données réelles
                // jeph et lg ont IsAdministrator=true, les autres false
                var isAdmin = u.TryGetProperty("Policy", out var policy)
                    && policy.TryGetProperty("IsAdministrator", out var adminProp)
                    && adminProp.GetBoolean();

                // ✅ IsDisabled — skip les comptes désactivés
                var isDisabled = u.TryGetProperty("Policy", out var policy2)
                    && policy2.TryGetProperty("IsDisabled", out var disabledProp)
                    && disabledProp.GetBoolean();

                if (isDisabled) continue;

                // ✅ Jellyfin n'expose PAS l'email — on génère comme Seerr le fait
                // Format : username@jellyfin.local (simple et prévisible)
                var email = $"{name.ToLower().Trim()}@jellyfin.local";

                users.Add(new ServerUser(
                    ServerId: jellyfinId,
                    Name: name,
                    Email: email,
                    IsAdmin: isAdmin
                ));
            }

            return users;
        }

        private async Task<List<ServerUser>> GetPlexUsersAsync(string url, string apiKey)
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("X-Plex-Token", apiKey);
            http.DefaultRequestHeaders.Add("Accept", "application/json");

            var response = await http.GetAsync($"{url}/accounts");
            if (!response.IsSuccessStatusCode) return [];

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            var users = new List<ServerUser>();

            var accounts = json
                .GetProperty("MediaContainer")
                .GetProperty("Account");

            foreach (var u in accounts.EnumerateArray())
            {
                users.Add(new ServerUser(
                    ServerId: u.GetProperty("id").GetInt32().ToString(),
                    Name: u.GetProperty("name").GetString() ?? "",
                    Email: u.TryGetProperty("email", out var e)
                        ? e.GetString() ?? "" : "",
                    IsAdmin: u.TryGetProperty("homeAdmin", out var ha)
                        && ha.GetBoolean()
                ));
            }

            return users;
        }
        // ─── Étape 3a : Radarr (optionnel) ───────────────────────────

        [HttpPost("radarr")]
        public async Task<IActionResult> ConfigureRadarr([FromBody] SetupArrDto dto)
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            var admin = admins.FirstOrDefault();
            if (admin is null) return BadRequest(new { Error = "Setup non initialisé" });

            // ✅ Test connexion
            var radarr = HttpContext.RequestServices.GetRequiredService<RadarrService>();
            var ok = await radarr.TestConnectionAsync(dto.Url, dto.ApiKey);
            if (!ok)
                return BadRequest(new { Error = "Connexion Radarr impossible — vérifie l'URL et l'API key" });

            var existing = await _db.UserRadarr
                .FirstOrDefaultAsync(r => r.UserId == admin.Id);

            if (existing is null)
            {
                _db.UserRadarr.Add(new UserRadarr
                {
                    Id = Guid.NewGuid(),
                    UserId = admin.Id,
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
            return Ok(new { Message = "Radarr configuré", NextStep = "sonarr" });
        }

        [HttpPost("radarr/skip")]
        public IActionResult SkipRadarr() =>
            Ok(new { Message = "Radarr ignoré", NextStep = "sonarr" });

        // ─── Étape 3b : Sonarr (optionnel) ───────────────────────────

        [HttpPost("sonarr")]
        public async Task<IActionResult> ConfigureSonarr([FromBody] SetupArrDto dto)
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            var admin = admins.FirstOrDefault();
            if (admin is null) return BadRequest(new { Error = "Setup non initialisé" });

            var sonarr = HttpContext.RequestServices.GetRequiredService<SonarrService>();
            var ok = await sonarr.TestConnectionAsync(dto.Url, dto.ApiKey);
            if (!ok)
                return BadRequest(new { Error = "Connexion Sonarr impossible — vérifie l'URL et l'API key" });

            var existing = await _db.UserSonarr
                .FirstOrDefaultAsync(s => s.UserId == admin.Id);

            if (existing is null)
            {
                _db.UserSonarr.Add(new UserSonarr
                {
                    Id = Guid.NewGuid(),
                    UserId = admin.Id,
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

            // ✅ Marque le setup comme terminé après Sonarr (dernière étape)
            var setting = await _db.AppSettings.FindAsync("IsSetupComplete");
            if (setting is null)
                _db.AppSettings.Add(new AppSettings { Key = "IsSetupComplete", Value = "true" });
            else
                setting.Value = "true";

            await _db.SaveChangesAsync();

            return Ok(new { Message = "Sonarr configuré — setup terminé !" });
        }

        [HttpPost("sonarr/skip")]
        public async Task<IActionResult> SkipSonarr()
        {
            // ✅ Marque quand même le setup comme terminé
            var setting = await _db.AppSettings.FindAsync("IsSetupComplete");
            if (setting is null)
                _db.AppSettings.Add(new AppSettings { Key = "IsSetupComplete", Value = "true" });
            else
                setting.Value = "true";

            await _db.SaveChangesAsync();
            return Ok(new { Message = "Setup terminé" });
        }

    }


    // ─── DTOs ─────────────────────────────────────────────────────────

    public record CreateAdminDto(string Email, string Password, string DisplayName);

    public record SetupServerDto(
        string FriendlyName,
        ServerType Type,
        string Url,
        string? ApiKey = null,
        string? Username = null,
        string? Password = null
    );

    public record SetupArrDto(string Url, string ApiKey);

    public record SetupSeerrDto(string Url, string ApiKey);

    internal record ServerUser(
        string ServerId,
        string Name,
        string Email,
        bool IsAdmin
    );
}