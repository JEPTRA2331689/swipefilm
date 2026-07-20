namespace swipefilm.Models
{
    public class UserProfile
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }

        // Vecteurs de préférences — JSON en BD
        public Dictionary<string, float> GenreWeights { get; set; } = [];
        public Dictionary<string, float> DirectorWeights { get; set; } = [];
        public Dictionary<string, float> ActorWeights { get; set; } = [];
        public Dictionary<string, float> KeywordWeights { get; set; } = [];
        public Dictionary<string, float> OriginalLanguageWeights { get; set; } = [];
        public Dictionary<string, float> PreferredDecadeWeights { get; set; } = [];
        public Dictionary<string, int> GenreCounts { get; set; } = [];
        public Dictionary<string, int> DirectorCounts { get; set; } = [];
        public Dictionary<string, int> ActorCounts { get; set; } = [];
        public Dictionary<string, int> KeywordCounts { get; set; } = [];
        public Dictionary<string, int> LanguageCounts { get; set; } = [];
        public float AvgCompletionRate { get; set; } = 0f;
        public float AvgUserRating { get; set; } = 0f;
        public int FavoriteCount { get; set; } = 0;
        public float RepeatViewRate { get; set; } = 0f;


        public float PreferredMinYear { get; set; } = DateTime.Now.Year - 5;
        public float PreferredRuntimeMax { get; set; } = 180;
        public float IndieVsBlockbuster { get; set; } = 0.5f; // 0=indie, 1=blockbuster
        public int TotalSignals { get; set; } = 0;
        public DateTime UpdatedAt { get; set; }

        // Navigation
        public User User { get; set; } = null!;
    }
}
