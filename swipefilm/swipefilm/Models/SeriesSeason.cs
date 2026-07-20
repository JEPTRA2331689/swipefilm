namespace swipefilm.Models
{
    public class SeriesSeason
    {
        public Guid Id { get; set; }
        public Guid SeriesId { get; set; }
        public int SeasonNumber { get; set; }
        public int EpisodeCount { get; set; }
        public int? TmdbSeasonId { get; set; }

        // Navigation
        public Series Series { get; set; } = null!;
    }
}
