// swipefilm/Auth/RequirePermissionAttribute.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using swipefilm.Models;

namespace swipefilm.Auth
{
    /// <summary>
    /// Vérifie qu'un utilisateur connecté possède la permission requise.
    /// Admin bypass automatiquement toutes les vérifications.
    /// Usage : [RequirePermission(Permission.ManageUsers)]
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class RequirePermissionAttribute : Attribute, IAuthorizationFilter
    {
        private readonly Permission _required;

        public RequirePermissionAttribute(Permission required)
        {
            _required = required;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;

            // Pas authentifié
            if (!user.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            // Lit les permissions depuis le claim JWT
            var perms = AuthManager.GetPermissions(user);

            if (!PermissionHelper.HasPermission(perms, _required))
            {
                context.Result = new ObjectResult(new
                {
                    error = "Permission insuffisante",
                    required = _required.ToString(),
                    code = 403
                })
                { StatusCode = 403 };
            }
        }
    }
}