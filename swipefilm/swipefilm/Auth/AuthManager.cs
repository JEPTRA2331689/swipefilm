using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using swipefilm.Models;

namespace swipefilm.Auth
{
    public class AuthManager
    {
        private readonly AppDbContext _db;

        public AuthManager(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Crée la session en base (source de vérité, révocable — voir
        /// Program.cs OnValidatePrincipal) et écrit le cookie. Point d'entrée
        /// unique utilisé par register/login/setup admin/auto-import Jellyfin.
        /// </summary>
        public async Task SignInAsync(HttpContext httpContext, User user)
        {
            var session = new AuthSession
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(30),
            };
            _db.AuthSessions.Add(session);
            await _db.SaveChangesAsync();

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email!),
                new Claim(ClaimTypes.Name, user.DisplayName),
                new Claim("permissions", user.Permissions.ToString()),
                new Claim("isAdmin", user.IsAdmin.ToString().ToLower()),
                new Claim("sid", session.Id.ToString()),
            };

            var identity = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme);

            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30),
                });
        }

        /// <summary>Lit les permissions depuis les claims de la session actuelle</summary>
        public static long GetPermissions(ClaimsPrincipal user)
        {
            var permClaim = user.FindFirstValue("permissions");
            return long.TryParse(permClaim, out var perms) ? perms : 0;
        }
    }
}
