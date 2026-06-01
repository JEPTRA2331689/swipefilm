namespace swipefilm.Models
{
    public class UserServer
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public ServerType Type { get; set; }
        public string FriendlyName { get; set; } = null!;
        public string UrlEncrypted { get; set; } = null!;
        public string TokenEncrypted { get; set; } = null!;
        public bool IsActive { get; set; } = true;
        public DateTime? LastSyncAt { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation
        public User User { get; set; } = null!;
        public ICollection<WatchHistory> WatchHistory { get; set; } = [];
    }

    public enum ServerType { Plex, Jellyfin }
}
