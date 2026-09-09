// swipefilm/Controllers/SettingsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using swipefilm.Auth;
using swipefilm.Models;

namespace swipefilm.Controllers
{
    /// <summary>Réglages d'instance transversaux — pas spécifiques à un
    /// service (Radarr/Sonarr/serveur média ont déjà leurs propres
    /// contrôleurs).</summary>
    [ApiController]
    [Route("api/settings")]
    [Authorize]
    [RequirePermission(Permission.Admin)]
    public class SettingsController : ControllerBase
    {
        private readonly IAppConfigService _config;

        public SettingsController(IAppConfigService config)
        {
            _config = config;
        }

        /// <summary>Adresse à laquelle Radarr/Sonarr peuvent joindre
        /// SwipeFilm pour le callback webhook — utile quand Radarr/Sonarr ne
        /// tourne pas sur la même machine que le navigateur de l'admin (sinon
        /// le callback déduit de l'URL du navigateur ne serait pas joignable
        /// depuis Radarr/Sonarr).</summary>
        [HttpGet("public-url")]
        public IActionResult GetPublicUrl()
        {
            return Ok(new { url = _config.GetPublicUrl() });
        }

        [HttpPut("public-url")]
        public async Task<IActionResult> SetPublicUrl([FromBody] PublicUrlDto dto)
        {
            await _config.SetPublicUrlAsync(dto.Url);
            return Ok(new { url = _config.GetPublicUrl() });
        }
    }

    public record PublicUrlDto(string? Url);
}
