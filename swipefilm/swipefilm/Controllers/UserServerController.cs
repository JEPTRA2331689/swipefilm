// swipefilm/Controllers/UserServerController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using swipefilm.Auth;
using swipefilm.Models;

namespace swipefilm.Controllers
{
    // ✅ Un seul serveur pour toute l'instance — plus une liste par
    // utilisateur. Seul un admin peut le configurer/resynchroniser.
    [ApiController]
    [Route("api/servers")]
    [Authorize]
    [RequirePermission(Permission.Admin)]
    public class UserServerController : ControllerBase
    {
        private readonly IAppConfigService _config;

        public UserServerController(IAppConfigService config)
        {
            _config = config;
        }

        [HttpGet]
        public IActionResult GetServer()
        {
            var server = _config.GetServer();
            if (server is null) return Ok((object?)null);

            return Ok(new
            {
                server.FriendlyName,
                server.Type,
                server.LastSyncAt,
            });
        }

        [HttpPost]
        public async Task<IActionResult> ConfigureServer([FromBody] AddServerDto dto)
        {
            try
            {
                var server = await _config.ConfigureServerAsync(dto);
                return Ok(new
                {
                    server.FriendlyName,
                    server.Type,
                    Message = "Serveur configuré — synchronisation démarrée en arrière-plan"
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { Error = ex.Message });
            }
        }

        // ✅ Test de connexion sans sauvegarder
        [HttpPost("test")]
        public async Task<IActionResult> TestConnection([FromBody] TestConnectionDto dto)
        {
            var ok = await _config.TestServerConnectionAsync(dto);
            return ok
                ? Ok(new { Success = true, Message = "Connexion réussie" })
                : BadRequest(new { Success = false, Message = "Connexion impossible" });
        }

        [HttpPost("sync")]
        public IActionResult TriggerSync()
        {
            // ✅ Déclenche une sync manuelle immédiate
            Hangfire.BackgroundJob.Enqueue<SyncBackgroundJobService>(
                "default",
                x => x.SyncSingleServerAsync());

            return Ok(new { Message = "Synchronisation démarrée" });
        }
    }
}
