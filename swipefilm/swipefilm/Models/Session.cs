namespace swipefilm.Models
{
    public class Session
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = null!;  // Code court type "X7K2"
        public SessionStatus Status { get; set; }
        public Guid CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }

        /// <summary>Genre(s) choisis par l'hôte seul — null tant que non défini.</summary>
        public string[]? GenreFilter { get; set; }
        public HomeContentFilter ContentTypeFilter { get; set; } = HomeContentFilter.All;

        /// <summary>
        /// Distingue "l'hôte n'a pas encore validé" de "l'hôte a démarré sans
        /// filtre de genre" (choix valide — pool tout-venant) : GenreFilter
        /// seul ne suffit pas à déduire la phase lobby/swipe.
        /// </summary>
        public bool HasStarted { get; set; } = false;

        /// <summary>
        /// L'hôte a cliqué "Lancer" — quitte l'écran de lobby (QR/code) pour
        /// l'écran de configuration (genre, durée). Distinct de HasStarted :
        /// entre les deux, la session est en configuration mais pas encore
        /// en phase swipe.
        /// </summary>
        public bool HasLaunched { get; set; } = false;

        // Navigation
        public User CreatedBy { get; set; } = null!;
        public ICollection<SessionMember> Members { get; set; } = [];
        public ICollection<SessionMatch> Matches { get; set; } = [];
        public ICollection<Swipe> Swipes { get; set; } = [];
    }

    public enum SessionStatus { Active, Completed, Expired }
}
