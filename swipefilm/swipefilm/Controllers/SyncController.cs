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
        private readonly AppDbContext _db;
        private readonly JellyfinService _jellyfin;
        private readonly PlexService _plex;
        private readonly ILogger<SyncController> _logger;  // ← type générique
        private readonly IServiceProvider _serviceProvider;

        public SyncController(
            AppDbContext db,
            JellyfinService jellyfin,
            PlexService plex,
            ILogger<SyncController> logger,  // ← type générique
            IServiceProvider serviceProvider)
        {
            _db = db;
            _jellyfin = jellyfin;
            _plex = plex;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);


        [HttpPost("{serverId}")]
        public async Task<IActionResult> SyncServer(Guid serverId)
        {
            var server = await _db.UserServers
                .FirstOrDefaultAsync(s =>
                    s.Id == serverId &&
                    s.UserId == CurrentUserId &&
                    s.IsActive);

            if (server is null) return NotFound();

            // ✅ Queue "critical" pour les syncs manuelles
            var jobId = BackgroundJob.Enqueue<SyncBackgroundJobService>(
                "critical",
                x => x.SyncSingleServerAsync(serverId));

            return Ok(new
            {
                message = "Sync en cours ⏳",
                jobId = jobId,
                trackUrl = $"/hangfire/jobs/details/{jobId}"
            });
        }
    }
}
