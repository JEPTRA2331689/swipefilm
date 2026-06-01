using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using swipefilm.Auth.swipefilm.Services;

namespace swipefilm.Controllers
{
    [ApiController]
    [Route("api/tmdb")]
    [Authorize]
    public class TmdbController : ControllerBase
    {
        private readonly TmdbService _tmdb;

        public TmdbController(TmdbService tmdb)
        {
            _tmdb = tmdb;
        }

        [HttpPost("enrich")]
        public async Task<IActionResult> EnrichAll()
        {
            await _tmdb.EnrichAllMoviesAsync();
            return Ok(new { message = "Enrichissement TMDB terminé ✅" });
        }
    }
}
