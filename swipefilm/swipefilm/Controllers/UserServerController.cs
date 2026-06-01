using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using swipefilm.Auth;

namespace swipefilm.Controllers
{
    // SwipeFilm.API/Controllers/UserServerController.cs
    [ApiController]
    [Route("api/servers")]
    [Authorize]
    public class UserServerController : ControllerBase
    {
        private readonly IUserServerService _serverService;

        public UserServerController(IUserServerService serverService)
        {
            _serverService = serverService;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<IActionResult> GetServers()
        {
            var servers = await _serverService.GetServersAsync(CurrentUserId);

            // ⚠️ On ne retourne JAMAIS les champs chiffrés au client
            return Ok(servers.Select(s => new
            {
                s.Id,
                s.FriendlyName,
                s.Type,
                s.IsActive,
                s.LastSyncAt,
                s.CreatedAt
            }));
        }

        [HttpPost]
        public async Task<IActionResult> AddServer([FromBody] AddServerDto dto)
        {
            try
            {
                var server = await _serverService.AddServerAsync(CurrentUserId, dto);
                return Ok(new { server.Id, server.FriendlyName, server.Type });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{serverId}")]
        public async Task<IActionResult> DeleteServer(Guid serverId)
        {
            try
            {
                await _serverService.DeleteServerAsync(CurrentUserId, serverId);
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }
    }
}
