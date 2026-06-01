namespace swipefilm.Models
{
    public class UserSeerr
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string UrlEncrypted { get; set; } = null!;
        public string ApiKeyEncrypted { get; set; } = null!;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }

        // Navigation
        public User User { get; set; } = null!;
    }
}
