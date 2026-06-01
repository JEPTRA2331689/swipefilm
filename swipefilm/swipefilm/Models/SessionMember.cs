namespace swipefilm.Models
{
    public class SessionMember
    {
        public Guid Id { get; set; }
        public Guid SessionId { get; set; }
        public Guid UserId { get; set; }
        public DateTime JoinedAt { get; set; }

        // Navigation
        public Session Session { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}
