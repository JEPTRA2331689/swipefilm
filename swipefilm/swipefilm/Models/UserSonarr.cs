namespace swipefilm.Models
{
    public class UserSonarr
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string UrlEncrypted { get; set; } = null!;
        public string ApiKeyEncrypted { get; set; } = null!;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }

        // ✅ Préférences sauvegardées
        public int? DefaultQualityProfileId { get; set; }
        public string? DefaultQualityProfileName { get; set; }
        public string? DefaultRootFolderPath { get; set; }

        public User User { get; set; } = null!;
    }
}
