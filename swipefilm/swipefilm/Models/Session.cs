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

        // Navigation
        public User CreatedBy { get; set; } = null!;
        public ICollection<SessionMember> Members { get; set; } = [];
        public ICollection<SessionMatch> Matches { get; set; } = [];
    }

    public enum SessionStatus { Active, Completed, Expired }
}
