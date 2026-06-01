using Hangfire;
using Microsoft.EntityFrameworkCore;
using swipefilm.Auth.swipefilm.Services;
using swipefilm.Data;
using swipefilm.Models;

namespace swipefilm.Auth
{
    public class SyncBackgroundJobService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<SyncBackgroundJobService> _logger;

        public SyncBackgroundJobService(
            IServiceProvider services,
            ILogger<SyncBackgroundJobService> logger)
        {
            _services = services;
            _logger = logger;
        }

        // ─── Sync tous les serveurs ───────────────────────────────────

        public async Task SyncAllServersAsync()
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var servers = await db.UserServers
                .Where(s => s.IsActive)
                .ToListAsync();

            _logger.LogInformation(
                "[Hangfire] Sync démarrée pour {Count} serveurs", servers.Count);

            foreach (var server in servers)
            {
                try
                {
                    // ✅ Enqueue sans queue spécifique
                    BackgroundJob.Enqueue<SyncBackgroundJobService>(
                        x => x.SyncSingleServerAsync(server.Id));

                    // ✅ Enqueue avec queue spécifique
                    BackgroundJob.Enqueue<SyncBackgroundJobService>(
                        "default",
                        x => x.SyncSingleServerAsync(server.Id));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[Hangfire] Erreur enqueue serveur {Name}",
                        server.FriendlyName);
                }
            }
        }

        // ─── Sync un serveur spécifique ───────────────────────────────

        public async Task SyncSingleServerAsync(Guid serverId)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var server = await db.UserServers.FindAsync(serverId);
            if (server is null || !server.IsActive) return;

            IMediaServerService mediaService = server.Type == ServerType.Jellyfin
                ? scope.ServiceProvider.GetRequiredService<JellyfinService>()
                : scope.ServiceProvider.GetRequiredService<PlexService>();

            var syncService = new SyncService(db, scope.ServiceProvider);

            // ✅ Sync incrémentale — passe LastSyncAt
            await syncService.SyncServerAsync(server, mediaService, server.LastSyncAt);

            // ✅ Enrichir les nouveaux films après la sync
            var tmdbService = scope.ServiceProvider.GetRequiredService<TmdbService>();
            await tmdbService.EnrichAllMoviesAsync();

            // ✅ Sync statuts Seerr
            var seerrService = scope.ServiceProvider.GetRequiredService<SeerrService>();
            await seerrService.SyncRequestStatusesAsync(server.UserId);

            _logger.LogInformation(
                "[Hangfire] Sync terminée pour {Name}", server.FriendlyName);
        }

        // ─── Discovery pour tous les users ───────────────────────────

        public async Task DiscoverForAllUsersAsync()
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var userIds = await db.Users
                .Select(u => u.Id)
                .ToListAsync();

            var discovery = scope.ServiceProvider
                .GetRequiredService<PersonalizedDiscoveryService>();

            foreach (var userId in userIds)
            {
                try
                {
                    await discovery.DiscoverForUserAsync(userId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[Hangfire] Erreur discovery user {UserId}", userId);
                }
            }
        }
    }
}
