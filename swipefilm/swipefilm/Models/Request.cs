namespace swipefilm.Models
{
    public class Request
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid MovieId { get; set; }
        public string? OverseerrRequestId { get; set; }
        public RequestStatus Status { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? AvailableAt { get; set; }

        // Navigation
        public User User { get; set; } = null!;
        public Movie Movie { get; set; } = null!;
    }

    public enum RequestStatus
    {
        Pending,
        Approved,
        Downloading,
        Available,
        Rejected
    }
}
