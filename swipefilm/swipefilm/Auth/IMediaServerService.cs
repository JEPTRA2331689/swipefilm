namespace swipefilm.Auth
{
    public interface IMediaServerService
    {
        Task<List<MediaItem>> GetLibraryAsync(string? userId);
        Task<List<MediaItem>> GetLibraryIncrementalAsync(DateTime? since, string? userId);
        Task<List<WatchHistoryItem>> GetWatchHistoryAsync(string? userId);
        Task<List<WatchHistoryItem>> GetWatchHistoryIncrementalAsync(DateTime? since, string? userId);
        Task<string> GetStreamUrlAsync(string itemId);

        /// <summary>
        /// Liste les saisons d'une série avec leur état de possession/complétion
        /// en une seule requête (l'API Plex/Jellyfin retourne déjà viewedLeafCount/
        /// PlayedPercentage par saison) — sert à la fois à ServerSeriesSeason
        /// (quelles saisons sont possédées) et SeriesWatchHistory (à quel point
        /// elles sont regardées).
        /// </summary>
        Task<List<SeasonItem>> GetSeasonsAsync(string seriesServerId, string? userId);

        /// <summary>
        /// ServerId (Jellyfin) / ratingKey de la série (Plex) des séries ayant eu
        /// une activité de visionnage depuis `since` — permet à
        /// SyncSeriesWatchHistoryForUserAsync de ne rappeler GetSeasonsAsync que
        /// pour les séries concernées au lieu de toute la bibliothèque à chaque
        /// passage. `since` null → tout est concerné (première sync).
        /// </summary>
        Task<HashSet<string>> GetRecentlyWatchedSeriesServerIdsAsync(DateTime? since, string? userId);
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

    // Une saison d'une série — possession + complétion en un seul DTO
    public record SeasonItem(
        string ServerId,           // ID de la saison dans Plex/Jellyfin
        int SeasonNumber,
        int EpisodeCount,
        int WatchedEpisodeCount,
        bool IsFavorite,
        int? UserRating,
        DateTime LastWatchedAt,
        DateTime FirstWatchedAt,
        string? ContentHash = null
    );
}
