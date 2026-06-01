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

        public float PreferredMinYear { get; set; } = 1970;
        public float PreferredRuntimeMax { get; set; } = 180;
        public float IndieVsBlockbuster { get; set; } = 0.5f; // 0=indie, 1=blockbuster
        public int TotalSignals { get; set; } = 0;
        public DateTime UpdatedAt { get; set; }

        // Navigation
        public User User { get; set; } = null!;
    }
}
