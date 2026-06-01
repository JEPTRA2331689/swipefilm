namespace swipefilm.Models
{
    public class SessionMatch
    {
        public Guid Id { get; set; }
        public Guid SessionId { get; set; }
        public Guid MovieId { get; set; }
        public DateTime MatchedAt { get; set; }

        // Navigation
        public Session Session { get; set; } = null!;
        public Movie Movie { get; set; } = null!;
    }
}
