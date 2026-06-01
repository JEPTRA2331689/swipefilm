using System.ComponentModel.DataAnnotations.Schema;

namespace swipefilm.Models
{
    // SwipeFilm.Infrastructure/Models/RefreshToken.cs
    public class RefreshToken
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Token { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsRevoked { get; set; }

        // Navigation
        public User User { get; set; } = null!;

        [NotMapped]
        public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

        [NotMapped]
        public bool IsValid => !IsRevoked && !IsExpired;
    }
}
