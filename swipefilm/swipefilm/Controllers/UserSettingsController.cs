// swipefilm/Controllers/UserSettingsController.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using swipefilm.Auth;
using swipefilm.Models;

namespace swipefilm.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize]
    public class UserSettingsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly UserManager<User> _userManager;

        public UserSettingsController(
            AppDbContext db,
            UserManager<User> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private long CurrentUserPermissions =>
            AuthManager.GetPermissions(User);

        // ─── GET /api/users/me ────────────────────────────────────────
        // Profil complet de l'utilisateur connecté

        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Id == CurrentUserId);

            if (user is null) return Unauthorized();

            return Ok(MapToDto(user));
        }

        // ─── PUT /api/users/me ────────────────────────────────────────
        // Mettre à jour son propre profil

        [HttpPut("me")]
        public async Task<IActionResult> UpdateMe([FromBody] UpdateUserDto dto)
        {
            var user = await _db.Users.FindAsync(CurrentUserId);
            if (user is null) return Unauthorized();

            if (!string.IsNullOrWhiteSpace(dto.DisplayName))
                user.DisplayName = dto.DisplayName.Trim();

            if (!string.IsNullOrWhiteSpace(dto.AvatarUrl))
                user.AvatarUrl = dto.AvatarUrl.Trim();

            await _db.SaveChangesAsync();

            // ✅ Plus besoin de "régénérer un token" comme avec le JWT — les
            // claims de session se rafraîchissent depuis la base à chaque
            // requête (voir Program.cs, OnValidatePrincipal).
            return Ok(new
            {
                Message = "Profil mis à jour",
                User = MapToDto(user)
            });
        }

        // ─── GET /api/users/me/preferences ───────────────────────────
        // Préférences d'affichage et de comportement

        [HttpGet("me/preferences")]
        public async Task<IActionResult> GetPreferences()
        {
            var user = await _db.Users.FindAsync(CurrentUserId);
            if (user is null) return Unauthorized();

            return Ok(new
            {
                Locale = user.Locale ?? "en",
                Region = user.Region ?? "US",
                OriginalLanguage = user.OriginalLanguage ?? "en",
                AutoRequestOnSwipe = user.AutoRequestOnSwipe,
                DiscordId = user.DiscordId,
                TelegramChatId = user.TelegramChatId
            });
        }

        // ─── PUT /api/users/me/preferences ───────────────────────────
        // Mettre à jour ses préférences

        [HttpPut("me/preferences")]
        public async Task<IActionResult> UpdatePreferences(
            [FromBody] UpdatePreferencesDto dto)
        {
            var user = await _db.Users.FindAsync(CurrentUserId);
            if (user is null) return Unauthorized();

            if (dto.Locale is not null)
                user.Locale = dto.Locale.ToLower().Trim();

            if (dto.Region is not null)
                user.Region = dto.Region.ToUpper().Trim();

            if (dto.OriginalLanguage is not null)
                user.OriginalLanguage = dto.OriginalLanguage.ToLower().Trim();

            if (dto.AutoRequestOnSwipe is not null)
                user.AutoRequestOnSwipe = dto.AutoRequestOnSwipe.Value;

            if (dto.DiscordId is not null)
                user.DiscordId = string.IsNullOrWhiteSpace(dto.DiscordId)
                    ? null
                    : dto.DiscordId.Trim();

            if (dto.TelegramChatId is not null)
                user.TelegramChatId = string.IsNullOrWhiteSpace(dto.TelegramChatId)
                    ? null
                    : dto.TelegramChatId.Trim();

            await _db.SaveChangesAsync();
            return Ok(new { Message = "Préférences mises à jour" });
        }

        // ─── GET /api/users/me/quota ──────────────────────────────────
        // Quota de l'utilisateur connecté

        [HttpGet("me/quota")]
        public async Task<IActionResult> GetMyQuota()
        {
            var user = await _db.Users.FindAsync(CurrentUserId);
            if (user is null) return Unauthorized();

            // ✅ Récupère le quota global comme fallback
            var globalMovieLimit = await GetGlobalQuotaAsync("GlobalMovieQuotaLimit");
            var globalMovieDays = await GetGlobalQuotaAsync("GlobalMovieQuotaDays");
            var globalTvLimit = await GetGlobalQuotaAsync("GlobalTvQuotaLimit");
            var globalTvDays = await GetGlobalQuotaAsync("GlobalTvQuotaDays");

            // ✅ Quota effectif : user override > global > null (illimité)
            var effectiveMovieLimit = user.MovieQuotaLimit ?? globalMovieLimit;
            int? effectiveMovieDays = user.MovieQuotaDays ?? globalMovieDays ?? 7;
            var effectiveTvLimit = user.TvQuotaLimit ?? globalTvLimit;
            int? effectiveTvDays = user.TvQuotaDays ?? globalTvDays ?? 7;

            // ✅ Compte les requêtes dans la période effective — même logique
            // que MediaRequestService.CheckQuotaAsync (source de vérité : la
            // table MediaRequest, pas la config Radarr/Sonarr).
            int? moviesUsed = null;
            int? tvUsed = null;

            if (effectiveMovieLimit is not null)
            {
                var since = DateTime.UtcNow.AddDays(-(effectiveMovieDays ?? 7));
                moviesUsed = await _db.MediaRequests.CountAsync(r =>
                    r.UserId == CurrentUserId
                    && r.Type == MediaRequestType.Movie
                    && r.Status != RequestStatus.Declined
                    && r.RequestedAt > since);
            }

            if (effectiveTvLimit is not null)
            {
                var since = DateTime.UtcNow.AddDays(-(effectiveTvDays ?? 7));
                tvUsed = await _db.MediaRequests.CountAsync(r =>
                    r.UserId == CurrentUserId
                    && r.Type == MediaRequestType.Tv
                    && r.Status != RequestStatus.Declined
                    && r.RequestedAt > since);
            }

            return Ok(new
            {
                Movie = new
                {
                    Limit = effectiveMovieLimit,       // null = illimité
                    Days = effectiveMovieLimit is null ? null : effectiveMovieDays,
                    Used = moviesUsed,
                    Remaining = effectiveMovieLimit is null
                        ? null
                        : (int?)(effectiveMovieLimit - moviesUsed),
                    IsUnlimited = effectiveMovieLimit is null
                },
                Tv = new
                {
                    Limit = effectiveTvLimit,
                    Days = effectiveTvLimit is null ? null : effectiveTvDays,
                    Used = tvUsed,
                    Remaining = effectiveTvLimit is null
                        ? null
                        : (int?)(effectiveTvLimit - tvUsed),
                    IsUnlimited = effectiveTvLimit is null
                }
            });
        }

        // ─── PUT /api/users/me/password ───────────────────────────────
        // Changer son mot de passe (users locaux seulement)

        [HttpPut("me/password")]
        public async Task<IActionResult> ChangePassword(
            [FromBody] ChangePasswordDto dto)
        {
            var user = await _userManager.FindByIdAsync(CurrentUserId.ToString());
            if (user is null) return Unauthorized();

            // ✅ Jellyfin users n'ont pas de vrai mot de passe SwipeFilm
            if (user.JellyfinUserId is not null)
                return BadRequest(new
                {
                    error = "Les utilisateurs Jellyfin gèrent leur mot de passe directement sur Jellyfin"
                });

            var result = await _userManager.ChangePasswordAsync(
                user, dto.CurrentPassword, dto.NewPassword);

            if (!result.Succeeded)
                return BadRequest(new
                {
                    error = "Mot de passe incorrect",
                    errors = result.Errors.Select(e => e.Description)
                });

            return Ok(new { Message = "Mot de passe mis à jour" });
        }

        // ═══════════════════════════════════════════════════════════════
        // ADMIN — gestion des autres utilisateurs
        // ═══════════════════════════════════════════════════════════════

        // ─── GET /api/users ───────────────────────────────────────────
        // Liste tous les utilisateurs

        [HttpGet]
        [RequirePermission(Permission.ManageUsers)]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _db.Users
                .OrderBy(u => u.DisplayName)
                .ToListAsync();

            return Ok(users.Select(MapToDto));
        }

        // ─── GET /api/users/{userId} ──────────────────────────────────
        // Détail d'un utilisateur spécifique

        [HttpGet("{userId:guid}")]
        [RequirePermission(Permission.ManageUsers)]
        public async Task<IActionResult> GetUser(Guid userId)
        {
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user is null) return NotFound();
            return Ok(MapToDto(user));
        }

        // ─── PUT /api/users/{userId} ──────────────────────────────────
        // Modifier un utilisateur (admin)

        [HttpPut("{userId:guid}")]
        [RequirePermission(Permission.ManageUsers)]
        public async Task<IActionResult> UpdateUser(
            Guid userId, [FromBody] UpdateUserDto dto)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user is null) return NotFound();

            // ✅ Impossible de modifier un Admin si tu n'es pas Admin
            if (user.IsAdmin && !PermissionHelper.HasPermission(
                CurrentUserPermissions, Permission.Admin))
                return StatusCode(403, new
                {
                    error = "Impossible de modifier un administrateur"
                });

            if (!string.IsNullOrWhiteSpace(dto.DisplayName))
                user.DisplayName = dto.DisplayName.Trim();

            if (!string.IsNullOrWhiteSpace(dto.AvatarUrl))
                user.AvatarUrl = dto.AvatarUrl.Trim();

            await _db.SaveChangesAsync();
            return Ok(new { Message = "Utilisateur mis à jour", User = MapToDto(user) });
        }

        // ─── GET /api/users/{userId}/preferences ──────────────────────
        // Préférences d'un user spécifique (admin)

        [HttpGet("{userId:guid}/preferences")]
        [RequirePermission(Permission.ManageUsers)]
        public async Task<IActionResult> GetUserPreferences(Guid userId)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user is null) return NotFound();

            return Ok(new
            {
                Locale = user.Locale ?? "en",
                Region = user.Region ?? "US",
                OriginalLanguage = user.OriginalLanguage ?? "en",
                AutoRequestOnSwipe = user.AutoRequestOnSwipe,
                DiscordId = user.DiscordId,
                TelegramChatId = user.TelegramChatId
            });
        }

        // ─── PUT /api/users/{userId}/preferences ──────────────────────
        // Modifier les préférences d'un user (admin)

        [HttpPut("{userId:guid}/preferences")]
        [RequirePermission(Permission.ManageUsers)]
        public async Task<IActionResult> UpdateUserPreferences(
            Guid userId, [FromBody] UpdatePreferencesDto dto)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user is null) return NotFound();

            if (dto.Locale is not null)
                user.Locale = dto.Locale.ToLower().Trim();

            if (dto.Region is not null)
                user.Region = dto.Region.ToUpper().Trim();

            if (dto.OriginalLanguage is not null)
                user.OriginalLanguage = dto.OriginalLanguage.ToLower().Trim();

            if (dto.AutoRequestOnSwipe is not null)
                user.AutoRequestOnSwipe = dto.AutoRequestOnSwipe.Value;

            if (dto.DiscordId is not null)
                user.DiscordId = string.IsNullOrWhiteSpace(dto.DiscordId)
                    ? null : dto.DiscordId.Trim();

            if (dto.TelegramChatId is not null)
                user.TelegramChatId = string.IsNullOrWhiteSpace(dto.TelegramChatId)
                    ? null : dto.TelegramChatId.Trim();

            await _db.SaveChangesAsync();
            return Ok(new { Message = "Préférences mises à jour" });
        }

        // ─── GET /api/users/{userId}/quota ────────────────────────────
        // Quota d'un user spécifique (admin)

        [HttpGet("{userId:guid}/quota")]
        [RequirePermission(Permission.ManageUsers)]
        public async Task<IActionResult> GetUserQuota(Guid userId)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user is null) return NotFound();

            return Ok(new
            {
                Movie = new
                {
                    Limit = user.MovieQuotaLimit,
                    Days = user.MovieQuotaDays,
                    IsUnlimited = user.MovieQuotaLimit is null,
                    IsOverride = user.MovieQuotaLimit is not null
                },
                Tv = new
                {
                    Limit = user.TvQuotaLimit,
                    Days = user.TvQuotaDays,
                    IsUnlimited = user.TvQuotaLimit is null,
                    IsOverride = user.TvQuotaLimit is not null
                }
            });
        }

        // ─── PUT /api/users/{userId}/quota ────────────────────────────
        // Modifier le quota d'un user (admin)

        [HttpPut("{userId:guid}/quota")]
        [RequirePermission(Permission.ManageUsers)]
        public async Task<IActionResult> UpdateUserQuota(
            Guid userId, [FromBody] UpdateQuotaDto dto)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user is null) return NotFound();

            // ✅ null = reset vers le global (illimité par défaut)
            user.MovieQuotaLimit = dto.MovieQuotaLimit;
            user.MovieQuotaDays = dto.MovieQuotaLimit.HasValue
                ? (dto.MovieQuotaDays ?? 7)
                : null;

            user.TvQuotaLimit = dto.TvQuotaLimit;
            user.TvQuotaDays = dto.TvQuotaLimit.HasValue
                ? (dto.TvQuotaDays ?? 7)
                : null;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                Message = dto.MovieQuotaLimit is null && dto.TvQuotaLimit is null
                    ? "Quota remis à illimité"
                    : "Quota mis à jour",
                Movie = new { Limit = user.MovieQuotaLimit, Days = user.MovieQuotaDays },
                Tv = new { Limit = user.TvQuotaLimit, Days = user.TvQuotaDays }
            });
        }

        // ─── DELETE /api/users/{userId} ───────────────────────────────
        // Désactiver un utilisateur (soft delete)

        [HttpDelete("{userId:guid}")]
        [RequirePermission(Permission.ManageUsers)]
        public async Task<IActionResult> DeleteUser(Guid userId)
        {
            // ✅ Impossible de se supprimer soi-même
            if (userId == CurrentUserId)
                return BadRequest(new { error = "Impossible de supprimer votre propre compte" });

            var user = await _db.Users.FindAsync(userId);
            if (user is null) return NotFound();

            // ✅ Impossible de supprimer un Admin sans être Admin
            if (user.IsAdmin && !PermissionHelper.HasPermission(
                CurrentUserPermissions, Permission.Admin))
                return StatusCode(403, new
                {
                    error = "Impossible de supprimer un administrateur"
                });

            // ✅ Soft delete — désactive sans supprimer les données
            user.Permissions = (long)Permission.None;
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.MaxValue;

            await _db.SaveChangesAsync();
            return Ok(new { Message = "Utilisateur désactivé" });
        }

        // ─── Helpers ──────────────────────────────────────────────────

        private async Task<int?> GetGlobalQuotaAsync(string key)
        {
            var setting = await _db.AppSettings.FindAsync(key);
            return int.TryParse(setting?.Value, out var v) ? v : null;
        }

        private static object MapToDto(User user) => new
        {
            user.Id,
            user.Email,
            user.DisplayName,
            user.AvatarUrl,
            user.CreatedAt,
            user.LastLoginAt,
            user.Permissions,
            IsAdmin = user.IsAdmin,
            user.JellyfinUserId,
            user.PlexUserId,
            user.Locale,
            user.Region,
            user.OriginalLanguage,
            user.AutoRequestOnSwipe,
            user.DiscordId,
            user.TelegramChatId,
            Quota = new
            {
                MovieQuotaLimit = user.MovieQuotaLimit,
                MovieQuotaDays = user.MovieQuotaDays,
                TvQuotaLimit = user.TvQuotaLimit,
                TvQuotaDays = user.TvQuotaDays,
                IsMovieUnlimited = user.MovieQuotaLimit is null,
                IsTvUnlimited = user.TvQuotaLimit is null
            }
        };
    }

    // ─── DTOs ─────────────────────────────────────────────────────────

    public record UpdateUserDto(
        string? DisplayName,
        string? AvatarUrl
    );

    public record UpdatePreferencesDto(
        string? Locale,
        string? Region,
        string? OriginalLanguage,
        bool? AutoRequestOnSwipe,
        string? DiscordId,
        string? TelegramChatId
    );

    public record UpdateQuotaDto(
        int? MovieQuotaLimit,
        int? MovieQuotaDays,
        int? TvQuotaLimit,
        int? TvQuotaDays
    );

    public record ChangePasswordDto(
        string CurrentPassword,
        string NewPassword
    );
}