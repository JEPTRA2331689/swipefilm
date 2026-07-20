// swipefilm/Models/Permission.cs
namespace swipefilm.Models
{
    [Flags]
    public enum Permission : long
    {
        // ─── Aucune permission ────────────────────────────────────────
        None = 0,

        // ─── Administration ───────────────────────────────────────────
        /// <summary>Accès total — bypass toutes les vérifications</summary>
        Admin = 1 << 1,          // 2

        /// <summary>Peut gérer les autres utilisateurs (rôles, permissions)</summary>
        ManageUsers = 1 << 3,    // 8

        // ─── Requêtes Radarr/Sonarr ───────────────────────────────────
        /// <summary>Peut ajouter un film/série à Radarr/Sonarr</summary>
        CanRequest = 1 << 5,     // 32

        /// <summary>Ses requêtes sont envoyées directement sans approbation admin</summary>
        AutoApprove = 1 << 7,    // 128

        /// <summary>Peut voir les requêtes des autres utilisateurs</summary>
        ViewRequests = 1 << 10,  // 1024

        /// <summary>Peut approuver ou refuser les requêtes des autres</summary>
        ManageRequests = 1 << 11, // 2048

        // ─── Application SwipeFilm ────────────────────────────────────
        /// <summary>Peut utiliser l'app de swipe et voir les recommandations</summary>
        CanSwipe = 1 << 12,      // 4096

        /// <summary>Peut voir l'historique de visionnage des autres</summary>
        ViewOthersHistory = 1 << 13, // 8192

        /// <summary>Peut accéder au dashboard admin</summary>
        ViewAdminDashboard = 1 << 14, // 16384

        // ─── Combinaisons prédéfinies ─────────────────────────────────
        /// <summary>User standard — peut swiper et faire des requêtes</summary>
        DefaultUser = CanSwipe | CanRequest,

        /// <summary>Modérateur — peut gérer les requêtes sans être admin complet</summary>
        Moderator = CanSwipe | CanRequest | AutoApprove | ViewRequests | ManageRequests,

        /// <summary>Admin complet — tous les droits</summary>
        FullAdmin = Admin | ManageUsers | CanRequest | AutoApprove |
                    ViewRequests | ManageRequests | CanSwipe |
                    ViewOthersHistory | ViewAdminDashboard,
    }

    /// <summary>
    /// Vérifie si un utilisateur possède une permission.
    /// Si l'utilisateur a Admin, retourne toujours true (comme Overseerr).
    /// </summary>
    public static class PermissionHelper
    {
        public static bool HasPermission(long userPermissions, Permission required)
        {
            // Admin bypass tout
            if ((userPermissions & (long)Permission.Admin) != 0)
                return true;

            return (userPermissions & (long)required) != 0;
        }

        public static long AddPermission(long current, Permission toAdd)
            => current | (long)toAdd;

        public static long RemovePermission(long current, Permission toRemove)
            => current & ~(long)toRemove;

        public static bool HasAnyPermission(long userPermissions, params Permission[] permissions)
            => permissions.Any(p => HasPermission(userPermissions, p));

        public static bool HasAllPermissions(long userPermissions, params Permission[] permissions)
            => permissions.All(p => HasPermission(userPermissions, p));
    }
}