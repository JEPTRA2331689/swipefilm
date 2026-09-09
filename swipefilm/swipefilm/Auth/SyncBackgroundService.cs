// swipefilm/Auth/SyncBackgroundService.cs
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

        // ✅ Plus de _db injecté — AppDbContext est Scoped, créé dans le scope
        public SyncBackgroundJobService(
            IServiceProvider services,
            ILogger<SyncBackgroundJobService> logger)
        {
            _services = services;
            _logger = logger;
        }

        // ✅ Un seul serveur pour toute l'instance — plus une boucle sur N
        // serveurs, juste un alias vers l'unique sync (gardé pour ne pas
        // avoir à changer l'enregistrement du cron dans Program.cs).
        public Task SyncAllServersAsync() => SyncSingleServerAsync();

        public async Task SyncSingleServerAsync()
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var config = scope.ServiceProvider.GetRequiredService<IAppConfigService>();

            var server = config.GetServer();
            if (server is null) return;

            IMediaServerService mediaService = server.Type == ServerType.Jellyfin
                ? scope.ServiceProvider.GetRequiredService<JellyfinService>()
                : scope.ServiceProvider.GetRequiredService<PlexService>();

            // ✅ SyncService sans injection — stateless
            var syncService = new SyncService();

            await syncService.SyncServerAsync(
                server, mediaService, config, server.LastSyncAt, db);

            var tmdbService = scope.ServiceProvider.GetRequiredService<TmdbService>();
            await tmdbService.EnrichAllMoviesAsync();

            // ✅ C'est ici, juste après le scan Jellyfin/Plex, qu'on sait ce
            // qui est réellement lisible — c'est ce qui confirme le passage
            // Downloading → Available/PartiallyAvailable, pour tout le monde.
            var requestService = scope.ServiceProvider.GetRequiredService<MediaRequestService>();
            await requestService.ConfirmAvailabilityAsync();

            _logger.LogInformation(
                "[Hangfire] Sync terminée pour {Name}", server.FriendlyName);
        }

        /// <summary>
        /// Enchaîne sync + calcul du profil + discover pour un utilisateur qui
        /// vient d'être importé (setup initial ou import ultérieur) — pour
        /// qu'il ait des recommandations dès sa première visite, pas après le
        /// prochain passage du job de sync horaire.
        /// </summary>
        public async Task OnboardNewUserAsync(Guid userId)
        {
            await SyncSingleServerAsync();

            using var scope = _services.CreateScope();
            var engine = scope.ServiceProvider.GetRequiredService<RecommendationEngine>();
            await engine.UpdateProfileFromHistoryAsync(userId);
            await engine.UpdateSeriesProfileFromHistoryAsync(userId);

            var discovery = scope.ServiceProvider.GetRequiredService<PersonalizedDiscoveryService>();
            await discovery.DiscoverForUserAsync(userId);

            _logger.LogInformation(
                "[Hangfire] Onboarding (sync + profil + discover) terminé pour user {UserId}", userId);
        }

        /// <summary>
        /// Filet de sécurité derrière les webhooks Radarr/Sonarr — au cas où
        /// un événement serait manqué (webhook down, Radarr redémarré...).
        /// </summary>
        public async Task SyncAllRequestStatusesAsync()
        {
            using var scope = _services.CreateScope();
            var requestService = scope.ServiceProvider.GetRequiredService<MediaRequestService>();

            try
            {
                await requestService.SyncRequestStatusesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Hangfire] Erreur sync statuts requêtes");
            }
        }

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