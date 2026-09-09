// swipefilm/Auth/SyncService.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using swipefilm.Data;
using swipefilm.Models;

namespace swipefilm.Auth
{
    public class SyncService
    {
        // ✅ Plus d'IHttpContextAccessor — inutilisable dans Hangfire
        public SyncService() { }

        // ─── Point d'entrée ───────────────────────────────────────────
        // Un seul serveur pour toute l'instance : la bibliothèque (quels
        // films/séries/saisons existent) est un fait système, synchronisée
        // une seule fois. L'historique de visionnage reste per-user — on
        // boucle sur chaque utilisateur ayant un compte sur ce serveur.

        public async Task SyncServerAsync(
            ServerSettings server,
            IMediaServerService mediaService,
            IAppConfigService config,
            DateTime? since,
            AppDbContext db)
        {
            // ✅ Récupéré une seule fois — nécessaire pour construire un lien
            // direct vers une fiche Plex (voir MovieController/SeriesController)
            string? newMachineIdentifier = null;
            if (server.Type == ServerType.Plex
                && string.IsNullOrEmpty(server.MachineIdentifier)
                && mediaService is PlexService plexForIdentifier)
            {
                newMachineIdentifier = await plexForIdentifier.GetMachineIdentifierAsync();
            }

            var users = await db.Users
                .Where(u => server.Type == ServerType.Jellyfin
                    ? u.JellyfinUserId != null
                    : u.PlexUserId != null)
                .ToListAsync();

            // ✅ Bibliothèque — la liste des films/séries ne dépend pas de
            // qui regarde, donc un seul passage suffit. On emprunte l'id
            // du premier utilisateur disponible juste pour que l'appel API
            // soit valide (Jellyfin exige un contexte utilisateur).
            var libraryUserId = server.Type == ServerType.Jellyfin
                ? users.FirstOrDefault()?.JellyfinUserId
                : users.FirstOrDefault()?.PlexUserId;

            await SyncLibraryAsync(mediaService, libraryUserId, since, db);

            // ✅ Historique — vraiment per-user, même si le serveur ne l'est plus
            foreach (var user in users)
            {
                var userServerId = server.Type == ServerType.Jellyfin
                    ? user.JellyfinUserId : user.PlexUserId;

                await SyncWatchHistoryForUserAsync(user.Id, mediaService, userServerId, since, db);
                await SyncSeriesWatchHistoryForUserAsync(user.Id, mediaService, userServerId, since, db);
            }

            await config.UpdateServerSyncInfoAsync(newMachineIdentifier, DateTime.UtcNow);
        }

        // ─── Sync bibliothèque (système-wide) ──────────────────────────

        private async Task SyncLibraryAsync(
            IMediaServerService mediaService,
            string? libraryUserId,
            DateTime? since,
            AppDbContext db)
        {
            List<MediaItem> items;

            if (since.HasValue && mediaService is JellyfinService jellyfin)
                items = await jellyfin.GetLibraryIncrementalAsync(since, libraryUserId);
            else if (since.HasValue && mediaService is PlexService plex)
                items = await plex.GetLibraryIncrementalAsync(since, libraryUserId);
            else
                items = await mediaService.GetLibraryAsync(libraryUserId);

            Console.WriteLine($"[Sync] Items reçus: {items.Count}");

            if (!items.Any())
            {
                Console.WriteLine("[Sync] Bibliothèque — aucun nouveau film");
                return;
            }

            var movieItems = items.Where(i => i.Type == "movie").ToList();
            var seriesItems = items.Where(i => i.Type != "movie").ToList();

            await SyncMoviesAsync(movieItems, db);

            if (seriesItems.Any())
                await SyncSeriesAsync(mediaService, seriesItems, libraryUserId, db);
        }

        private async Task SyncMoviesAsync(List<MediaItem> items, AppDbContext db)
        {
            if (!items.Any()) return;

            // ✅ TmdbIds existants en une requête
            var existingMovieTmdbIds = (await db.Movies
                .Select(m => m.TmdbId)
                .ToListAsync())
                .ToHashSet();

            // ✅ ServerMovies existants — une ligne par film, système-wide
            // (chargés en entités, pas juste les IDs, pour pouvoir backfiller
            // ServerItemId sur les lignes créées avant l'ajout de ce champ).
            var existingServerMovies = await db.ServerMovie
                .ToDictionaryAsync(sm => sm.MovieId, sm => sm);

            var newMovies = new List<Movie>();

            foreach (var item in items)
            {
                if (item.TmdbId is null || !int.TryParse(item.TmdbId, out var tmdbId))
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

            if (newMovies.Any())
            {
                db.Movies.AddRange(newMovies);
                await db.SaveChangesAsync();
                Console.WriteLine($"[Sync] {newMovies.Count} nouveaux films ajoutés");
            }

            var tmdbIdsFromItems = items
                .Where(i => i.TmdbId != null)
                .Select(i => int.Parse(i.TmdbId!))
                .ToHashSet();

            var allMovies = await db.Movies
                .Where(m => tmdbIdsFromItems.Contains(m.TmdbId))
                .ToDictionaryAsync(m => m.TmdbId, m => m.Id);

            var newServerMovies = new List<ServerMovie>();
            var backfilled = 0;

            foreach (var item in items)
            {
                if (item.TmdbId is null || !int.TryParse(item.TmdbId, out var tmdbId))
                    continue;

                if (!allMovies.TryGetValue(tmdbId, out var movieId)) continue;

                if (existingServerMovies.TryGetValue(movieId, out var existing))
                {
                    // ✅ Backfill — ligne créée avant l'ajout de ServerItemId
                    if (string.IsNullOrEmpty(existing.ServerItemId))
                    {
                        existing.ServerItemId = item.ServerId;
                        backfilled++;
                    }
                    continue;
                }

                var serverMovie = new ServerMovie
                {
                    Id = Guid.NewGuid(),
                    MovieId = movieId,
                    ServerItemId = item.ServerId,
                };
                newServerMovies.Add(serverMovie);
                existingServerMovies[movieId] = serverMovie;
            }

            if (newServerMovies.Any() || backfilled > 0)
            {
                if (newServerMovies.Any()) db.ServerMovie.AddRange(newServerMovies);
                await db.SaveChangesAsync();
                Console.WriteLine($"[Sync] {newServerMovies.Count} ServerMovies ajoutés, {backfilled} ServerItemId rétro-remplis");
            }
        }

        // ─── Sync séries (système-wide) ─────────────────────────────────

        private async Task SyncSeriesAsync(
            IMediaServerService mediaService,
            List<MediaItem> items,
            string? libraryUserId,
            AppDbContext db)
        {
            var existingSeriesTmdbIds = (await db.Series
                .Select(s => s.TmdbId)
                .ToListAsync())
                .ToHashSet();

            var existingServerSeries = await db.ServerSeries
                .ToDictionaryAsync(ss => ss.SeriesId, ss => ss);

            var newSeries = new List<Series>();

            foreach (var item in items)
            {
                if (item.TmdbId is null || !int.TryParse(item.TmdbId, out var tmdbId))
                    continue;

                if (existingSeriesTmdbIds.Contains(tmdbId)) continue;

                newSeries.Add(new Series
                {
                    Id = Guid.NewGuid(),
                    TmdbId = tmdbId,
                    Title = item.Title,
                    CachedAt = DateTime.MinValue
                });

                existingSeriesTmdbIds.Add(tmdbId);
            }

            if (newSeries.Any())
            {
                db.Series.AddRange(newSeries);
                await db.SaveChangesAsync();
                Console.WriteLine($"[Sync] {newSeries.Count} nouvelles séries ajoutées");
            }

            var tmdbIdsFromItems = items
                .Where(i => i.TmdbId != null)
                .Select(i => int.Parse(i.TmdbId!))
                .ToHashSet();

            var allSeries = await db.Series
                .Where(s => tmdbIdsFromItems.Contains(s.TmdbId))
                .ToDictionaryAsync(s => s.TmdbId, s => s.Id);

            var newServerSeries = new List<ServerSeries>();
            var seriesBackfilled = 0;

            foreach (var item in items)
            {
                if (item.TmdbId is null || !int.TryParse(item.TmdbId, out var tmdbId))
                    continue;

                if (!allSeries.TryGetValue(tmdbId, out var seriesId)) continue;

                if (existingServerSeries.TryGetValue(seriesId, out var existing))
                {
                    if (string.IsNullOrEmpty(existing.ServerItemId))
                    {
                        existing.ServerItemId = item.ServerId;
                        seriesBackfilled++;
                    }
                    continue;
                }

                var serverSeries = new ServerSeries
                {
                    Id = Guid.NewGuid(),
                    SeriesId = seriesId,
                    ServerItemId = item.ServerId,
                };
                newServerSeries.Add(serverSeries);
                existingServerSeries[seriesId] = serverSeries;
            }

            if (newServerSeries.Any() || seriesBackfilled > 0)
            {
                if (newServerSeries.Any()) db.ServerSeries.AddRange(newServerSeries);
                await db.SaveChangesAsync();
                Console.WriteLine($"[Sync] {newServerSeries.Count} ServerSeries ajoutés, {seriesBackfilled} ServerItemId rétro-remplis");
            }

            // ✅ Existence des saisons — un appel par série (emprunte
            // libraryUserId, seule l'existence/le nombre d'épisodes nous
            // intéresse ici, pas la complétion — celle-ci est resynchronisée
            // par utilisateur dans SyncSeriesWatchHistoryForUserAsync).
            foreach (var item in items)
            {
                if (item.TmdbId is null || !int.TryParse(item.TmdbId, out var tmdbId))
                    continue;
                if (!allSeries.TryGetValue(tmdbId, out var seriesId))
                    continue;

                var seasonItems = await mediaService.GetSeasonsAsync(item.ServerId, libraryUserId);

                if (!seasonItems.Any()) continue;

                await SyncSeasonExistenceAsync(seriesId, seasonItems, db);
            }
        }

        private async Task SyncSeasonExistenceAsync(
            Guid seriesId, List<SeasonItem> seasonItems, AppDbContext db)
        {
            var existingSeasons = await db.SeriesSeasons
                .Where(s => s.SeriesId == seriesId)
                .ToDictionaryAsync(s => s.SeasonNumber, s => s);

            var newSeasons = new List<SeriesSeason>();

            foreach (var item in seasonItems)
            {
                if (existingSeasons.TryGetValue(item.SeasonNumber, out var existing))
                {
                    existing.EpisodeCount = item.EpisodeCount;
                    continue;
                }

                var season = new SeriesSeason
                {
                    Id = Guid.NewGuid(),
                    SeriesId = seriesId,
                    SeasonNumber = item.SeasonNumber,
                    EpisodeCount = item.EpisodeCount
                };
                newSeasons.Add(season);
                existingSeasons[item.SeasonNumber] = season;
            }

            if (newSeasons.Any())
                db.SeriesSeasons.AddRange(newSeasons);

            await db.SaveChangesAsync();

            // ✅ Possession — quelles saisons sont présentes (système-wide)
            var existingServerSeasonIds = (await db.ServerSeriesSeasons
                .Select(sss => sss.SeriesSeasonId)
                .ToListAsync())
                .ToHashSet();

            var newServerSeasons = seasonItems
                .Select(item => existingSeasons[item.SeasonNumber])
                .Where(s => !existingServerSeasonIds.Contains(s.Id))
                .Select(s => new ServerSeriesSeason
                {
                    Id = Guid.NewGuid(),
                    SeriesSeasonId = s.Id
                })
                .ToList();

            if (newServerSeasons.Any())
                db.ServerSeriesSeasons.AddRange(newServerSeasons);

            await db.SaveChangesAsync();
        }

        // ─── Sync historique films (par utilisateur) ───────────────────

        private async Task SyncWatchHistoryForUserAsync(
            Guid userId,
            IMediaServerService mediaService,
            string? userServerId,
            DateTime? since,
            AppDbContext db)
        {
            List<WatchHistoryItem> history;

            if (since.HasValue && mediaService is JellyfinService jellyfin)
                history = await jellyfin.GetWatchHistoryIncrementalAsync(since, userServerId);
            else if (since.HasValue && mediaService is PlexService plex)
                history = await plex.GetWatchHistoryIncrementalAsync(since, userServerId);
            else
                history = await mediaService.GetWatchHistoryAsync(userServerId);

            Console.WriteLine($"[Sync] {history.Count} items dans l'historique");

            if (!history.Any()) return;

            var allMovies = await db.Movies
                .ToDictionaryAsync(m => m.TmdbId, m => m);

            var existingHistory = await db.WatchHistory
                .Where(w => w.UserId == userId)
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
                    skipped++;
                    continue;
                }

                if (existingHistory.TryGetValue(movie.Id, out var existing))
                {
                    if (existing.ContentHash is not null && existing.ContentHash == item.ContentHash)
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
                        UserId = userId,
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
                db.WatchHistory.AddRange(toAdd);

            await db.SaveChangesAsync();

            Console.WriteLine(
                $"[Sync] WatchHistory — ajoutés: {added}, " +
                $"mis à jour: {updated}, " +
                $"inchangés: {unchanged}, " +
                $"ignorés: {skipped}");
        }

        // ─── Sync historique saisons (par utilisateur) ──────────────────

        private async Task SyncSeriesWatchHistoryForUserAsync(
            Guid userId,
            IMediaServerService mediaService,
            string? userServerId,
            DateTime? since,
            AppDbContext db)
        {
            if (string.IsNullOrEmpty(userServerId)) return;

            // ✅ Toutes les séries présentes dans la bibliothèque (système-wide)
            var seriesInLibrary = await db.ServerSeries
                .Where(ss => ss.ServerItemId != null)
                .Select(ss => new { ss.SeriesId, ss.ServerItemId })
                .ToListAsync();

            if (!seriesInLibrary.Any()) return;

            // ✅ En incrémental, ne rappelle GetSeasonsAsync (un appel réseau par
            // série) que pour les séries ayant eu de l'activité depuis `since` —
            // avant ce filtre, TOUTE la bibliothèque était rescannée à chaque
            // passage (toutes les 5 min via Hangfire), peu importe `since`.
            if (since.HasValue)
            {
                var recentlyTouched = await mediaService
                    .GetRecentlyWatchedSeriesServerIdsAsync(since, userServerId);

                seriesInLibrary = seriesInLibrary
                    .Where(s => recentlyTouched.Contains(s.ServerItemId!))
                    .ToList();

                if (!seriesInLibrary.Any())
                {
                    Console.WriteLine("[Sync] SeriesWatchHistory — aucune série avec activité récente");
                    return;
                }
            }

            var existingHistory = await db.SeriesWatchHistory
                .Where(w => w.UserId == userId)
                .ToDictionaryAsync(w => w.SeriesSeasonId, w => w);

            var newHistory = new List<SeriesWatchHistory>();

            foreach (var series in seriesInLibrary)
            {
                var seasonItems = await mediaService.GetSeasonsAsync(series.ServerItemId!, userServerId);
                if (!seasonItems.Any()) continue;

                var localSeasons = await db.SeriesSeasons
                    .Where(s => s.SeriesId == series.SeriesId)
                    .ToDictionaryAsync(s => s.SeasonNumber, s => s.Id);

                foreach (var item in seasonItems)
                {
                    if (!localSeasons.TryGetValue(item.SeasonNumber, out var seasonId))
                        continue;

                    // Rien regardé et pas favori → pas la peine de garder une ligne d'historique
                    if (item.WatchedEpisodeCount == 0 && !item.IsFavorite) continue;

                    if (existingHistory.TryGetValue(seasonId, out var existing))
                    {
                        if (existing.ContentHash is not null && existing.ContentHash == item.ContentHash)
                            continue;

                        existing.WatchedEpisodeCount = item.WatchedEpisodeCount;
                        existing.IsFavorite = item.IsFavorite;
                        existing.UserRating = item.UserRating;
                        existing.LastWatchedAt = item.LastWatchedAt;
                        existing.ContentHash = item.ContentHash;
                    }
                    else
                    {
                        newHistory.Add(new SeriesWatchHistory
                        {
                            Id = Guid.NewGuid(),
                            UserId = userId,
                            SeriesSeasonId = seasonId,
                            WatchedEpisodeCount = item.WatchedEpisodeCount,
                            IsFavorite = item.IsFavorite,
                            UserRating = item.UserRating,
                            FirstWatchedAt = item.FirstWatchedAt,
                            LastWatchedAt = item.LastWatchedAt,
                            ContentHash = item.ContentHash
                        });
                    }
                }
            }

            if (newHistory.Any())
                db.SeriesWatchHistory.AddRange(newHistory);

            await db.SaveChangesAsync();

            Console.WriteLine(
                $"[Sync] SeriesWatchHistory — {seriesInLibrary.Count} série(s) vérifiée(s), " +
                $"{newHistory.Count} nouvelle(s) ligne(s) d'historique");
        }
    }
}
