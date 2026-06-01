namespace swipefilm.Auth
{
    public interface IMediaServerService
    {
        Task<List<MediaItem>> GetLibraryAsync(Guid serverId);
        Task<List<MediaItem>> GetLibraryIncrementalAsync(Guid serverId, DateTime? since);
        Task<List<WatchHistoryItem>> GetWatchHistoryAsync(Guid serverId);
        Task<List<WatchHistoryItem>> GetWatchHistoryIncrementalAsync(Guid serverId, DateTime? since);
        Task<string> GetStreamUrlAsync(Guid serverId, string itemId);
    }

    // DTO commun Plex + Jellyfin
    public record MediaItem(
        string ServerId,        // ID dans Plex/Jellyfin
        string? TmdbId,         // Pour matcher avec TMDB
        string? ImdbId,
        string Title,
        int? Year,
        string Type,            // movie | series
        string? PosterUrl,
        int? RuntimeMinutes
    );

    public record WatchHistoryItem(
        string ServerId,        // ID dans Plex/Jellyfin
        string? TmdbId,
        string Title,
        int WatchDurationSec,
        int? AbandonedAtSec,
        int ViewCount,
        bool IsFavorite,
        int? UserRating,        // 1-10
        DateTime LastWatchedAt,
        DateTime FirstWatchedAt,
        string? ContentHash = null
    );
}
