// SwipeFilm.Infrastructure/Models/User.cs
using Microsoft.AspNetCore.Identity;
using swipefilm.Models;

public class User : IdentityUser<Guid>
{
    // IdentityUser fournit déjà :
    // Id, Email, PasswordHash, UserName, etc.

    // On ajoute seulement nos champs custom
    public string DisplayName { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public long Permissions { get; set; } = (long)Permission.DefaultUser;
    public string? JellyfinUserId { get; set; }         // ✅ nouveau
    public string? PlexUserId { get; set; }             // ✅ nouveau


    /// <summary>Raccourci — true si l'user a le bit Admin</summary>
    public bool IsAdmin => PermissionHelper.HasPermission(Permissions, Permission.Admin);
    // Ajouter dans Models/User.cs

    // ─── Quotas Radarr/Sonarr ─────────────────────────────────────
    public int? MovieQuotaLimit { get; set; } = null; // null = illimité
    public int? MovieQuotaDays { get; set; } = null;
    public int? TvQuotaLimit { get; set; } = null;
    public int? TvQuotaDays { get; set; } = null;

    // ─── Préférences d'affichage ──────────────────────────────────
    /// <summary>Langue interface. null = hérite du global</summary>
    public string? Locale { get; set; }
    /// <summary>Région pour filtrer le contenu. ex: "FR", "US", "CA"</summary>
    public string? Region { get; set; }
    /// <summary>Langue originale préférée. ex: "fr", "en"</summary>
    public string? OriginalLanguage { get; set; }

    // ─── Notifications externes ───────────────────────────────────
    public string? DiscordId { get; set; }
    public string? TelegramChatId { get; set; }

    // ─── Comportement automatique ─────────────────────────────────
    /// <summary>Swipe droit → ajout automatique à Radarr si AutoApprove</summary>
    public bool AutoRequestOnSwipe { get; set; } = false;

    /// <summary>Compte éphémère provisionné pour rejoindre une session de
    /// groupe sans créer de vrai compte — nettoyable par job planifié.</summary>
    public bool IsGuest { get; set; } = false;
    /// <summary>Vérifie une permission spécifique</summary>
    public bool HasPermission(Permission permission)
        => PermissionHelper.HasPermission(Permissions, permission);
    // Navigation
    public UserSeerr? Seerr { get; set; }
    public ICollection<WatchHistory> WatchHistory { get; set; } = [];
    public ICollection<Swipe> Swipes { get; set; } = [];
    public ICollection<Watchlist> Watchlist { get; set; } = [];
    public UserProfile? Profile { get; set; }
    public UserSeriesProfile? SeriesProfile { get; set; }
    public ICollection<MediaRequest> Requests { get; set; } = []; public ICollection<Session> CreatedSessions { get; set; } = [];
    public ICollection<SessionMember> SessionMemberships { get; set; } = [];
}