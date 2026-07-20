namespace swipefilm.Models
{
    /// <summary>
    /// Profil de goût dédié aux séries — même structure que UserProfile,
    /// mais CreatorWeights remplace DirectorWeights (TMDB expose created_by
    /// pour les séries, pas de "Director" fiable au niveau show) et deux
    /// signaux propres à l'engagement multi-saisons.
    /// </summary>
    public class UserSeriesProfile
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }

        public Dictionary<string, float> GenreWeights { get; set; } = [];
        public Dictionary<string, float> CreatorWeights { get; set; } = [];
        public Dictionary<string, float> ActorWeights { get; set; } = [];
        public Dictionary<string, float> KeywordWeights { get; set; } = [];
        public Dictionary<string, float> OriginalLanguageWeights { get; set; } = [];
        public Dictionary<string, float> PreferredDecadeWeights { get; set; } = [];
        public Dictionary<string, int> GenreCounts { get; set; } = [];
        public Dictionary<string, int> CreatorCounts { get; set; } = [];
        public Dictionary<string, int> ActorCounts { get; set; } = [];
        public Dictionary<string, int> KeywordCounts { get; set; } = [];
        public Dictionary<string, int> LanguageCounts { get; set; } = [];

        /// <summary>Taux de complétion moyen sur les saisons regardées (0-100).</summary>
        public float AvgSeasonCompletionRate { get; set; } = 0f;

        /// <summary>Profondeur d'engagement moyenne — saisons regardées / saisons possédées.</summary>
        public float AvgSeasonsWatchedRatio { get; set; } = 0f;

        public int TotalSeriesSignals { get; set; } = 0;
        public DateTime UpdatedAt { get; set; }

        // Navigation
        public User User { get; set; } = null!;
    }
}
