namespace swipefilm.Models
{
    public class Series
    {
        public Guid Id { get; set; }
        public int TmdbId { get; set; }
        public int? TvdbId { get; set; }
        public string Title { get; set; } = null!;
        public string? OriginalTitle { get; set; }
        public string? OriginalLanguage { get; set; }
        public string? PosterPath { get; set; }
        public string? BackdropPath { get; set; }
        public string? Overview { get; set; }
        public DateOnly? FirstAirDate { get; set; }
        public float TmdbRating { get; set; }
        public float TmdbPopularity { get; set; }
        public int TmdbVoteCount { get; set; }
        public string[] Genres { get; set; } = [];
        public string[] Keywords { get; set; } = [];
        public string[] CreatedBy { get; set; } = [];
        public string[] CastTop5 { get; set; } = [];
        public int NumberOfSeasons { get; set; }
        public int NumberOfEpisodes { get; set; }
        public DateTime CachedAt { get; set; }

        // Navigation
        public ICollection<SeriesSeason> Seasons { get; set; } = [];
        public ICollection<Swipe> Swipes { get; set; } = [];
        public ICollection<Watchlist> Watchlists { get; set; } = [];
        public ICollection<MediaRequest> Requests { get; set; } = [];
        public ICollection<SessionMatch> SessionMatches { get; set; } = [];
    }
}
