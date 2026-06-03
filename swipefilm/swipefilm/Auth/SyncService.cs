using Microsoft.EntityFrameworkCore;
using swipefilm.Data;
using swipefilm.Models;

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
            DateTime? since = null)
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
            List<MediaItem> items;

            if (since.HasValue && mediaService is JellyfinService jellyfin)
                items = await jellyfin.GetLibraryIncrementalAsync(server.Id, since);
            else if (since.HasValue && mediaService is PlexService plex)
                items = await plex.GetLibraryIncrementalAsync(server.Id, since);
            else
                items = await mediaService.GetLibraryAsync(server.Id);

            Console.WriteLine($"[Sync] Items reçus de Jellyfin/Plex: {items.Count}");


            if (!items.Any())
            {
                Console.WriteLine("[Sync] Bibliothèque — aucun nouveau film");
                return;
            }

            var withTmdb = items.Count(i => i.TmdbId != null);
            var withoutTmdb = items.Count(i => i.TmdbId == null);
            Console.WriteLine($"[Sync] Avec TmdbId: {withTmdb}, Sans TmdbId: {withoutTmdb}");


            // ✅ TmdbIds existants en une requête
            var existingMovieTmdbIds = (await _db.Movies
                .Select(m => m.TmdbId)
                .ToListAsync())
                .ToHashSet();

            Console.WriteLine($"[Sync] Films déjà en BD: {existingMovieTmdbIds.Count}");


            // ✅ ServerMovies existants pour ce serveur
            var existingServerMovieIds = (await _db.ServerMovie
                .Where(sm => sm.ServerId == server.Id)
                .Select(sm => sm.MovieId)
                .ToListAsync())
                .ToHashSet();

            Console.WriteLine($"[Sync] ServerMovies déjà en BD: {existingServerMovieIds.Count}");


            var newMovies = new List<Movie>();

            foreach (var item in items)
            {
                if (item.TmdbId is null
                    || !int.TryParse(item.TmdbId, out var tmdbId))
                    continue;

                if (existingMovieTmdbIds.Contains(tmdbId)) continue;

                newMovies.Add(new Movie
                {
                    Id = Guid.NewGuid(),
                    TmdbId = tmdbId,
                    Title = item.Title,
                    ContentType = item.Type,
                    RuntimeMinutes = item.RuntimeMinutes,
                    CachedAt = DateTime.MinValue
                });

                existingMovieTmdbIds.Add(tmdbId);
            }

            // ✅ Sauvegarde les nouveaux films d'abord
            if (newMovies.Any())
            {
                _db.Movies.AddRange(newMovies);
                await _db.SaveChangesAsync();
                Console.WriteLine($"[Sync] {newMovies.Count} nouveaux films ajoutés");
            }

            // ✅ Charge tous les films correspondants aux items reçus
            var tmdbIdsFromItems = items
                .Where(i => i.TmdbId != null)
                .Select(i => int.Parse(i.TmdbId!))
                .ToHashSet();

            var allMovies = await _db.Movies
                .Where(m => tmdbIdsFromItems.Contains(m.TmdbId))
                .ToDictionaryAsync(m => m.TmdbId, m => m.Id);

            // ✅ Ajoute les ServerMovies manquants
            var newServerMovies = new List<ServerMovie>();

            foreach (var item in items)
            {
                if (item.TmdbId is null
                    || !int.TryParse(item.TmdbId, out var tmdbId))
                    continue;

                if (!allMovies.TryGetValue(tmdbId, out var movieId))
                    continue;

                if (existingServerMovieIds.Contains(movieId)) continue;

                newServerMovies.Add(new ServerMovie
                {
                    Id = Guid.NewGuid(),
                    ServerId = server.Id,
                    MovieId = movieId,
                });

                existingServerMovieIds.Add(movieId);
            }

            if (newServerMovies.Any())
            {
                _db.ServerMovie.AddRange(newServerMovies);
                await _db.SaveChangesAsync();
                Console.WriteLine($"[Sync] {newServerMovies.Count} ServerMovies ajoutés");
            }
        }

        // ─── Sync historique ──────────────────────────────────────────

        private async Task SyncWatchHistoryAsync(
            UserServer server,
            IMediaServerService mediaService,
            DateTime? since)
        {
            List<WatchHistoryItem> history;

            if (since.HasValue && mediaService is JellyfinService jellyfin)
                history = await jellyfin.GetWatchHistoryIncrementalAsync(server.Id, since);
            else if (since.HasValue && mediaService is PlexService plex)
                history = await plex.GetWatchHistoryIncrementalAsync(server.Id, since);
            else
                history = await mediaService.GetWatchHistoryAsync(server.Id);

            Console.WriteLine($"[Sync] {history.Count} items dans l'historique");

            if (!history.Any()) return;

            var allMovies = await _db.Movies
                .ToDictionaryAsync(m => m.TmdbId, m => m);

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

            if (toAdd.Any())
                _db.WatchHistory.AddRange(toAdd);

            await _db.SaveChangesAsync();

            Console.WriteLine(
                $"[Sync] WatchHistory — ajoutés: {added}, " +
                $"mis à jour: {updated}, " +
                $"inchangés: {unchanged}, " +
                $"ignorés: {skipped}");
        }
    }
}