// swipefilm/Controllers/JobsController.cs
using Hangfire;
using Hangfire.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using swipefilm.Auth;
using swipefilm.Auth.swipefilm.Services;
using swipefilm.Models;

namespace swipefilm.Controllers
{
    /// <summary>
    /// Permet à un admin de déclencher manuellement les jobs récurrents
    /// (au lieu d'attendre leur prochain passage planifié) — réservé
    /// Permission.Admin, ces jobs touchent tous les utilisateurs.
    /// </summary>
    [ApiController]
    [Route("api/jobs")]
    [Authorize]
    [RequirePermission(Permission.Admin)]
    public class JobsController : ControllerBase
    {
        // ✅ Doit matcher les Id des RecurringJob.AddOrUpdate dans Program.cs
        private static readonly (string Id, string Label, string Description)[] Definitions =
        {
            ("sync-all-servers", "Sync Jellyfin/Plex",
                "Scanne les bibliothèques de tous les serveurs actifs et confirme les disponibilités réelles."),
            ("enrich-movies", "Enrichir les films (TMDB)",
                "Complète cast, genres et notes manquants depuis TMDB."),
            ("discovery-all-users", "Discover pour tous les utilisateurs",
                "Recalcule le profil de recommandation et relance un discover par utilisateur."),
            ("sync-request-statuses", "Sync statuts des requêtes",
                "Filet de sécurité derrière les webhooks Radarr/Sonarr — repasse sur les requêtes Approved/Downloading."),
        };

        [HttpGet]
        public IActionResult GetJobs()
        {
            var recurring = JobStorage.Current.GetConnection().GetRecurringJobs();
            var byId = recurring.ToDictionary(r => r.Id);

            var result = Definitions.Select(d => new
            {
                d.Id,
                d.Label,
                d.Description,
                LastExecution = byId.TryGetValue(d.Id, out var r) ? r.LastExecution : null,
                NextExecution = byId.TryGetValue(d.Id, out var r2) ? r2.NextExecution : null,
            });

            return Ok(result);
        }

        [HttpPost("{id}/run")]
        public IActionResult RunJob(string id)
        {
            // ✅ Queue "critical" — même raisonnement que SyncController pour
            // les déclenchements manuels, ils passent devant la file "default"
            string jobId = id switch
            {
                "sync-all-servers" => BackgroundJob.Enqueue<SyncBackgroundJobService>(
                    "critical", x => x.SyncAllServersAsync()),
                "enrich-movies" => BackgroundJob.Enqueue<TmdbService>(
                    "critical", x => x.EnrichAllMoviesAsync()),
                "discovery-all-users" => BackgroundJob.Enqueue<SyncBackgroundJobService>(
                    "critical", x => x.DiscoverForAllUsersAsync()),
                "sync-request-statuses" => BackgroundJob.Enqueue<SyncBackgroundJobService>(
                    "critical", x => x.SyncAllRequestStatusesAsync()),
                _ => null!,
            };

            if (jobId is null)
                return NotFound(new { error = $"Job inconnu : {id}" });

            return Ok(new
            {
                message = "Job lancé",
                jobId,
                trackUrl = $"/hangfire/jobs/details/{jobId}"
            });
        }
    }
}
