using Microsoft.EntityFrameworkCore;
using swipefilm.Data;
using swipefilm.Models;
using swipefilm.Auth;

namespace swipefilm.Auth
{
    public class SyncService
    {
        private readonly AppDbContext _db;
        private readonly IServiceProvider _serviceProvider;

        public SyncService(AppDbContext db, IServiceProvider serviceProvider)
        {
            _db = db;
            _serviceProvider = serviceProvider;
        }

        // ─── Point d'entrée ───────────────────────────────────────────

        public async Task SyncServerAsync(
            UserServer server,
            IMediaServerService mediaService,
            DateTime? since = null) // ← sync incrémentale
        {
            await SyncLibraryAsync(server, mediaService, since);
            await SyncWatchHistoryAsync(server, mediaService, since);

            server.LastSyncAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        // ─── Sync bibliothèque ────────────────────────────────────────

        private async Task SyncLibraryAsync(
            UserServer server,
            IMediaServerService mediaService,
            DateTime? since)
        {
            // ✅ Sync incrémentale si possible
            List<MediaItem> items;

            if (since.HasValue && mediaService is JellyfinService jellyfin)
                items = await jellyfin.GetLibraryIncrementalAsync(server.Id, since);
            else if (since.HasValue && mediaService is PlexService plex)
                items = await plex.GetLibraryIncrementalAsync(server.Id, since);
            else
                items = await mediaService.GetLibraryAsync(server.Id);

            if (!items.Any())
            {
                Console.WriteLine("[Sync] Bibliothèque — aucun nouveau film");
                return;
            }

            // ✅ Une seule requête pour tous les TmdbIds existants
            var existingSet = (await _db.Movies
                .Select(m => m.TmdbId)
                .ToListAsync())
                .ToHashSet();

            var newMovies = new List<Movie>();

            foreach (var item in items)
            {
                if (item.TmdbId is null
                    || !int.TryParse(item.TmdbId, out var tmdbId))
                    continue;

                if (existingSet.Contains(tmdbId)) continue;

                newMovies.Add(new Movie
                {
                    Id = Guid.NewGuid(),
                    TmdbId = tmdbId,
                    Title = item.Title,
                    ContentType = item.Type,
                    RuntimeMinutes = item.RuntimeMinutes,
                    CachedAt = DateTime.MinValue
                });

                existingSet.Add(tmdbId); // Évite les doublons dans le batch
            }

            if (newMovies.Any())
            {
                // ✅ Batch insert — une seule transaction
                _db.Movies.AddRange(newMovies);
                await _db.SaveChangesAsync();
                Console.WriteLine($"[Sync] Bibliothèque — {newMovies.Count} nouveaux films ajoutés");
            }
        }

        // ─── Sync historique ──────────────────────────────────────────

        private async Task SyncWatchHistoryAsync(
            UserServer server,
            IMediaServerService mediaService,
            DateTime? since)
        {
            // ✅ Sync incrémentale si possible
            List<WatchHistoryItem> history;

            if (since.HasValue && mediaService is JellyfinService jellyfin)
                history = await jellyfin.GetWatchHistoryIncrementalAsync(server.Id, since);
            else if (since.HasValue && mediaService is PlexService plex)
                history = await plex.GetWatchHistoryIncrementalAsync(server.Id, since);
            else
                history = await mediaService.GetWatchHistoryAsync(server.Id);

            Console.WriteLine($"[Sync] {history.Count} items dans l'historique");

            if (!history.Any()) return;

            // ✅ Une seule requête pour tous les films
            var allMovies = await _db.Movies
                .ToDictionaryAsync(m => m.TmdbId, m => m);

            // ✅ Une seule requête pour tout le WatchHistory existant
            var existingHistory = await _db.WatchHistory
                .Where(w => w.UserId == server.UserId
                         && w.ServerId == server.Id)
                .ToDictionaryAsync(w => w.MovieId, w => w);

            int added = 0, updated = 0, skipped = 0, unchanged = 0;

            var toAdd = new List<WatchHistory>();

            foreach (var item in history)
            {
                if (item.TmdbId is null || !int.TryParse(item.TmdbId, out var tmdbId))
                {
                    skipped++;
                    continue;
                }

                if (!allMovies.TryGetValue(tmdbId, out var movie))
                {
                    Console.WriteLine($"[Sync] Film pas en BD : {item.Title} ({tmdbId})");
                    skipped++;
                    continue;
                }

                if (existingHistory.TryGetValue(movie.Id, out var existing))
                {
                    // ✅ Hash — skip si rien n'a changé
                    if (existing.ContentHash is not null
                        && existing.ContentHash == item.ContentHash)
                    {
                        unchanged++;
                        continue;
                    }

                    existing.WatchDurationSec = item.WatchDurationSec;
                    existing.AbandonedAtSec = item.AbandonedAtSec;
                    existing.ViewCount = item.ViewCount;
                    existing.IsFavorite = item.IsFavorite;
                    existing.UserRating = item.UserRating;
                    existing.LastWatchedAt = item.LastWatchedAt;
                    existing.ContentHash = item.ContentHash;
                    updated++;
                }
                else
                {
                    // ✅ Accumule les nouveaux dans une liste
                    toAdd.Add(new WatchHistory
                    {
                        Id = Guid.NewGuid(),
                        UserId = server.UserId,
                        ServerId = server.Id,
                        MovieId = movie.Id,
                        WatchDurationSec = item.WatchDurationSec,
                        AbandonedAtSec = item.AbandonedAtSec,
                        ViewCount = item.ViewCount,
                        IsFavorite = item.IsFavorite,
                        UserRating = item.UserRating,
                        FirstWatchedAt = item.FirstWatchedAt,
                        LastWatchedAt = item.LastWatchedAt,
                        ContentHash = item.ContentHash
                    });
                    added++;
                }
            }

            // ✅ Batch insert — une seule transaction pour tous les nouveaux
            if (toAdd.Any())
                _db.WatchHistory.AddRange(toAdd);

            // ✅ Une seule transaction pour tout
            await _db.SaveChangesAsync();

            Console.WriteLine(
                $"[Sync] WatchHistory — ajoutés: {added}, " +
                $"mis à jour: {updated}, " +
                $"inchangés: {unchanged}, " +
                $"ignorés: {skipped}");
        }
    }
}