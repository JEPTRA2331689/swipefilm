// swipefilm/Controllers/PermissionsController.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using swipefilm.Auth;
using swipefilm.Models;

namespace swipefilm.Controllers
{
    [ApiController]
    [Route("api/permissions")]
    [Authorize]
    public class PermissionsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public PermissionsController(AppDbContext db)
        {
            _db = db;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private long CurrentUserPermissions =>
            AuthManager.GetPermissions(User);

        // ─── Mes permissions ──────────────────────────────────────────

        [HttpGet("me")]
        public IActionResult GetMyPermissions()
        {
            var perms = CurrentUserPermissions;
            return Ok(new
            {
                Permissions = perms,
                PermissionsList = Enum.GetValues<Permission>()
                    .Where(p => p != Permission.None
                             && p != Permission.DefaultUser
                             && p != Permission.Moderator
                             && p != Permission.FullAdmin
                             && PermissionHelper.HasPermission(perms, p))
                    .Select(p => p.ToString())
                    .ToList()
            });
        }

        // ─── Modifier les permissions d'un user ───────────────────────

        [HttpPut("{userId}")]
        [RequirePermission(Permission.ManageUsers)]
        public async Task<IActionResult> SetPermissions(
            Guid userId, [FromBody] SetPermissionsDto dto)
        {
            var currentPerms = CurrentUserPermissions;
            var isCurrentAdmin = PermissionHelper.HasPermission(currentPerms, Permission.Admin);

            var target = await _db.Users.FindAsync(userId);
            if (target is null) return NotFound();

            if (target.IsAdmin && !isCurrentAdmin)
                return StatusCode(403, new
                {
                    error = "Impossible de modifier les permissions d'un Admin"
                });

            var newPermsHasAdmin = PermissionHelper.HasPermission(
                dto.Permissions, Permission.Admin);

            if (newPermsHasAdmin && !isCurrentAdmin)
                return StatusCode(403, new
                {
                    error = "Seul un Admin peut accorder le rôle Admin"
                });

            target.Permissions = dto.Permissions;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                UserId = userId,
                Permissions = target.Permissions,
                Message = "Permissions mises à jour"
            });
        }

        // ─── Appliquer un preset ──────────────────────────────────────

        [HttpPut("{userId}/preset")]
        [RequirePermission(Permission.ManageUsers)]
        public async Task<IActionResult> SetPreset(
            Guid userId, [FromBody] SetPresetDto dto)
        {
            var target = await _db.Users.FindAsync(userId);
            if (target is null) return NotFound();

            var currentPerms = CurrentUserPermissions;
            var isCurrentAdmin = PermissionHelper.HasPermission(currentPerms, Permission.Admin);

            // ✅ Switch statement au lieu de switch expression
            // — permet les early returns sans erreur de compilation
            long newPermissions;
            switch (dto.Preset.ToLower())
            {
                case "none":
                    newPermissions = (long)Permission.None;
                    break;

                case "user":
                    newPermissions = (long)Permission.DefaultUser;
                    break;

                case "moderator":
                    newPermissions = (long)Permission.Moderator;
                    break;

                case "admin":
                    if (!isCurrentAdmin)
                        return StatusCode(403, new
                        {
                            error = "Seul un Admin peut accorder le rôle Admin"
                        });
                    newPermissions = (long)Permission.FullAdmin;
                    break;

                default:
                    return BadRequest(new
                    {
                        error = "Preset inconnu",
                        validValues = new[] { "none", "user", "moderator", "admin" }
                    });
            }

            target.Permissions = newPermissions;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                UserId = userId,
                Preset = dto.Preset,
                Permissions = target.Permissions
            });
        }

        // ─── Permissions par défaut ───────────────────────────────────

        [HttpGet("defaults")]
        [RequirePermission(Permission.Admin)]
        public async Task<IActionResult> GetDefaults()
        {
            var setting = await _db.AppSettings.FindAsync("DefaultPermissions");
            var value = long.TryParse(setting?.Value, out var perms)
                ? perms
                : (long)Permission.DefaultUser;

            return Ok(new { DefaultPermissions = value });
        }

        [HttpPut("defaults")]
        [RequirePermission(Permission.Admin)]
        public async Task<IActionResult> SetDefaults([FromBody] SetDefaultsDto dto)
        {
            var setting = await _db.AppSettings.FindAsync("DefaultPermissions");

            if (setting is null)
                _db.AppSettings.Add(new AppSettings
                {
                    Key = "DefaultPermissions",
                    Value = dto.Permissions.ToString()
                });
            else
                setting.Value = dto.Permissions.ToString();

            await _db.SaveChangesAsync();
            return Ok(new { DefaultPermissions = dto.Permissions });
        }
    }

    public record SetPermissionsDto(long Permissions);
    public record SetPresetDto(string Preset);
    public record SetDefaultsDto(long Permissions);
}