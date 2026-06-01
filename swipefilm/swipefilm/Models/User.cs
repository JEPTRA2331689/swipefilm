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

    // Navigation
    public ICollection<UserServer> Servers { get; set; } = [];
    public UserSeerr? Seerr { get; set; }
    public ICollection<WatchHistory> WatchHistory { get; set; } = [];
    public ICollection<Swipe> Swipes { get; set; } = [];
    public ICollection<Watchlist> Watchlist { get; set; } = [];
    public UserProfile? Profile { get; set; }
    public ICollection<Request> Requests { get; set; } = [];
    public ICollection<Session> CreatedSessions { get; set; } = [];
    public ICollection<SessionMember> SessionMemberships { get; set; } = [];
}