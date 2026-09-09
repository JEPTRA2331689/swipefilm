using System.Security.Claims;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using swipefilm.Auth;
using swipefilm.Models;

namespace swipefilm.Controllers
{
    // swipefilm/Controllers/SyncController.cs
    [ApiController]
    [Route("api/sync")]
    [Authorize]
    public class SyncController : ControllerBase
    {
        private readonly IAppConfigService _config;
        private readonly JellyfinService _jellyfin;
        private readonly PlexService _plex;
        private readonly ILogger<SyncController> _logger;  // ← type générique
        private readonly IServiceProvider _serviceProvider;

        public SyncController(
            IAppConfigService config,
            JellyfinService jellyfin,
            PlexService plex,
            ILogger<SyncController> logger,  // ← type générique
            IServiceProvider serviceProvider)
        {
            _config = config;
            _jellyfin = jellyfin;
            _plex = plex;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);


        [HttpPost]
        public async Task<IActionResult> SyncServer()
        {
            var server = _config.GetServer();
            if (server is null) return NotFound();

            // ✅ Queue "critical" pour les syncs manuelles
            var jobId = BackgroundJob.Enqueue<SyncBackgroundJobService>(
                "critical",
                x => x.SyncSingleServerAsync());

            return Ok(new
            {
                message = "Sync en cours ⏳",
                jobId = jobId,
                trackUrl = $"/hangfire/jobs/details/{jobId}"
            });
        }
    }
}
