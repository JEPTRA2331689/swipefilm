// swipefilm/Models/DiscoveryCache.cs
namespace swipefilm.Models
{
    /// <summary>
    /// Cache des films pour les sections à rafraîchissement périodique
    /// (Daily Discovery, Weekly Discovery).
    /// Évite de recalculer à chaque appel — les films restent
    /// identiques jusqu'à expiration.
    /// </summary>
    public class DiscoveryCache
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string SectionId { get; set; } = ""; // "daily_discovery" | "weekly_discovery"
        public AvailabilityFilter Availability { get; set; } = AvailabilityFilter.All;
        // ✅ Liste de TmdbIds (sérialisé JSON par EF)
        public List<int> TmdbIds { get; set; } = new();

        public DateTime GeneratedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}