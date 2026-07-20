using System.ComponentModel.DataAnnotations.Schema;

namespace swipefilm.Models
{
    /// <summary>
    /// Historique de visionnage au niveau saison — miroir de WatchHistory,
    /// mais une ligne par saison plutôt que par film. C'est la source du
    /// signal "combien de saisons regardées" / "à quel point terminées".
    /// </summary>
    public class SeriesWatchHistory
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid SeriesSeasonId { get; set; }

        public int WatchedEpisodeCount { get; set; }
        public int? UserRating { get; set; }
        public bool IsFavorite { get; set; }

        public DateTime FirstWatchedAt { get; set; }
        public DateTime LastWatchedAt { get; set; }

        [NotMapped]
        public float CompletionPct =>
            SeriesSeason is null || SeriesSeason.EpisodeCount == 0 ? 0 :
            (float)WatchedEpisodeCount / SeriesSeason.EpisodeCount * 100;

        // Navigation
        public User User { get; set; } = null!;
        public SeriesSeason SeriesSeason { get; set; } = null!;
        public string? ContentHash { get; set; }
    }
}
