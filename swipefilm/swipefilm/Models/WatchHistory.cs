using System.ComponentModel.DataAnnotations.Schema;

namespace swipefilm.Models
{
    public class WatchHistory
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid ServerId { get; set; }
        public Guid MovieId { get; set; }

        // Données brutes de Plex/Jellyfin
        public int WatchDurationSec { get; set; }  // viewOffset(ms)/1000 ou PlaybackPositionTicks/10_000_000
        public int? AbandonedAtSec { get; set; }   // null si film complété
        public int ViewCount { get; set; } = 1;
        public int? UserRating { get; set; }       // Note donnée dans Plex/Jellyfin (1-10)
        public bool IsFavorite { get; set; }       // Favori dans Plex/Jellyfin

        public DateTime FirstWatchedAt { get; set; }
        public DateTime LastWatchedAt { get; set; }

        // Propriétés calculées — jamais stockées en BD
        [NotMapped]
        public float CompletionPct =>
            Movie is null ? 0 :
            Movie.RuntimeMinutes is null ? 0 :
            (float)WatchDurationSec / (Movie.RuntimeMinutes.Value * 60) * 100;

        [NotMapped]
        public bool IsAbandoned =>
            CompletionPct < 70f && ViewCount == 1;

        // Navigation
        public User User { get; set; } = null!;
        public UserServer Server { get; set; } = null!;
        public Movie Movie { get; set; } = null!;
        public string? ContentHash { get; set; }
    }
}
