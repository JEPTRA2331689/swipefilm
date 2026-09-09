// swipefilm/Controllers/AuthController.cs
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using swipefilm.Auth;
using swipefilm.Models;

namespace swipefilm.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly AuthManager _authManager;
        private readonly AppDbContext _db;
        private readonly IAppConfigService _config;

        public AuthController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            AuthManager authManager,
            AppDbContext db,
            IAppConfigService config)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _authManager = authManager;
            _db = db;
            _config = config;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            var user = new User
            {
                Email = dto.Email,
                UserName = dto.Email,
                DisplayName = dto.DisplayName,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            await _authManager.SignInAsync(HttpContext, user);
            return Ok(BuildMeDto(user));
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            Console.WriteLine(dto);
            // ✅ Le serveur Jellyfin de l'instance — un seul, partagé par tous
            var anyServer = _config.GetServer() is { } s && s.Type == ServerType.Jellyfin
                ? s : null;

            if (anyServer is not null)
            {
                // ✅ Tente le login Jellyfin — récupère le vrai JellyfinUserId
                var (jellyfinUserId, success) = await TryLoginJellyfinAsync(
                    anyServer, dto.Username, dto.Password);

                if (success && jellyfinUserId is not null)
                {
                    // ✅ Cherche par JellyfinUserId — fiable même si l'email change
                    var jellyfinUser = await _db.Users
                        .FirstOrDefaultAsync(u => u.JellyfinUserId == jellyfinUserId);

                    if (jellyfinUser is not null)
                    {
                        await _authManager.SignInAsync(HttpContext, jellyfinUser);
                        return Ok(BuildMeDto(jellyfinUser));
                    }

                    // ✅ Auto-import si l'user Jellyfin n'est pas encore dans SwipeFilm
                    // (cas où un nouvel user Jellyfin rejoint après le setup)
                    var newUser = await AutoImportJellyfinUserAsync(
                        anyServer, jellyfinUserId, dto.Username);
                    if (newUser is not null)
                    {
                        await _authManager.SignInAsync(HttpContext, newUser);
                        return Ok(BuildMeDto(newUser));
                    }
                }
            }

            // ✅ Fallback login classique Identity (pour l'admin créé manuellement)
            var user = await _userManager.FindByEmailAsync(dto.Username)
                    ?? await _userManager.FindByNameAsync(dto.Username);

            if (user is null)
                return Unauthorized("Utilisateur introuvable");

            var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, false);
            if (!result.Succeeded)
                return Unauthorized("Email ou mot de passe incorrect");

            await _authManager.SignInAsync(HttpContext, user);
            return Ok(BuildMeDto(user));
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var sid = User.FindFirstValue("sid");
            if (sid is not null && Guid.TryParse(sid, out var sessionId))
            {
                var session = await _db.AuthSessions.FindAsync(sessionId);
                if (session is not null)
                {
                    _db.AuthSessions.Remove(session);
                    await _db.SaveChangesAsync();
                }
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok();
        }

        // ✅ Même forme que GET /me — réutilisée pour que register/login
        // renvoient directement l'utilisateur sans aller-retour supplémentaire
        // côté front.
        private object BuildMeDto(User user)
        {
            var config = _config.GetServer();
            var server = config is null ? null : new
            {
                config.FriendlyName,
                config.Type,
                config.LastSyncAt,
            };

            return new
            {
                user.Id,
                user.Email,
                user.DisplayName,
                user.AvatarUrl,
                user.CreatedAt,
                user.Permissions,
                Server = server,
            };
        }

        private async Task<bool> ValidateJellyfinCredentialsAsync(
            string url, string username, string password)
        {
            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add(
                    "X-Emby-Authorization",
                    "MediaBrowser Client=\"SwipeFilm\", Device=\"API\", DeviceId=\"swipefilm\", Version=\"1.0.0\""
                );

                var response = await http.PostAsJsonAsync(
                    $"{url}/Users/AuthenticateByName",
                    new { Username = username, Pw = password });

                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }
        private async Task<(string? jellyfinUserId, bool success)> TryLoginJellyfinAsync(
    ServerSettings server, string username, string password)
        {
            try
            {
                var (url, _) = _config.GetServerCredentials();
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add(
                    "X-Emby-Authorization",
                    $"MediaBrowser Client=\"SwipeFilm\", Device=\"API\", " +
                    $"DeviceId=\"BOT_swipefilm_{username}\", Version=\"1.0.0\""
                );

                var response = await http.PostAsJsonAsync(
                    $"{url}/Users/AuthenticateByName",
                    new { Username = username, Pw = password });

                if (!response.IsSuccessStatusCode) return (null, false);

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();

                // ✅ Jellyfin retourne User.Id dans la réponse d'auth — c'est le JellyfinUserId
                var jellyfinUserId = json
                    .GetProperty("User")
                    .GetProperty("Id")
                    .GetString();

                return (jellyfinUserId, true);
            }
            catch
            {
                return (null, false);
            }
        }
        private async Task<User?> AutoImportJellyfinUserAsync(
    ServerSettings server, string jellyfinUserId, string username)
        {
            try
            {
                var (url, apiKey) = _config.GetServerCredentials();
                using var http = new HttpClient();

                // Récupère les détails du user depuis l'API admin Jellyfin
                var response = await http.GetAsync(
                    $"{url}/Users/{jellyfinUserId}?api_key={apiKey}");

                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                var isAdmin = json.TryGetProperty("Policy", out var policy)
                    && policy.TryGetProperty("IsAdministrator", out var a)
                    && a.GetBoolean();

                var email = $"{username.ToLower()}@jellyfin.local";
                var (serverLocale, serverRegion) = await GetJellyfinServerLocaleAsync(url, apiKey);

                var emailConflict = await _userManager.FindByEmailAsync(email);
                if (emailConflict is not null)
                    email = $"{username.ToLower()}.{jellyfinUserId[..8]}@jellyfin.local";

                var newUser = new User
                {
                    Email = email,
                    UserName = email,
                    DisplayName = username,
                    CreatedAt = DateTime.UtcNow,
                    JellyfinUserId = jellyfinUserId
                };
                newUser.Locale = serverLocale;
                newUser.Region = serverRegion;
                newUser.OriginalLanguage = serverLocale;

                var tempPwd = $"{Guid.NewGuid():N}"[..12] + "Aa1!";
                var result = await _userManager.CreateAsync(newUser, tempPwd);
                if (!result.Succeeded) return null;

                await _userManager.AddToRoleAsync(newUser, isAdmin ? "Admin" : "User");

                // ✅ Plus besoin de lier un serveur — il y en a un seul,
                // partagé par tous (newUser.JellyfinUserId suffit).
                await _db.SaveChangesAsync();
                return newUser;
            }
            catch
            {
                return null;
            }
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



        [HttpGet("me")]
        [RequirePermission(Permission.CanSwipe)]
        public async Task<IActionResult> Me()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null) return Unauthorized();

            return Ok(BuildMeDto(user));
        }
    }

    public record RegisterDto(
        string Email,
        string Password,
        string DisplayName
    );
    public record LoginDto(string Username, string Password);
}