namespace swipefilm.Models
{
    public class Watchlist
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid MovieId { get; set; }
        public int Score { get; set; }            // Score calculé par l'algo, mis à jour en background
        public DateTime AddedAt { get; set; }
        public DateTime? RemindedAt { get; set; }

        // Navigation
        public User User { get; set; } = null!;
        public Movie Movie { get; set; } = null!;
    }
}
