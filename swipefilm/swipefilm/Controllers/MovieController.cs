// swipefilm/Controllers/MovieController.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using swipefilm.Auth;
using swipefilm.Auth.swipefilm.Services;
using swipefilm.Data;
using swipefilm.Models;

namespace swipefilm.Controllers
{
    [ApiController]
    [Route("api/movies")]
    [Authorize]
    public class MovieController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly TmdbService _tmdb;
        private readonly IEncryptionService _encryption;

        public MovieController(AppDbContext db, TmdbService tmdb, IEncryptionService encryption)
        {
            _db = db;
            _tmdb = tmdb;
            _encryption = encryption;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>
        /// Détail d'un film — champs sensibles à la région/langue (titre,
        /// synopsis, affiche, classification) récupérés en direct depuis TMDB
        /// avec les préférences de l'utilisateur connecté, jamais mis en cache
        /// en base (contrairement aux champs qui servent au scoring). Inclut
        /// la disponibilité et le statut de demande — évite au frontend de
        /// devoir faire des appels séparés pour afficher le bouton principal.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetMovie(Guid id)
        {
            var movie = await _db.Movies.FindAsync(id);
            if (movie is null) return NotFound();

            var user = await _db.Users.FindAsync(CurrentUserId);
            var region = user?.Region ?? "FR";
            var locale = $"{user?.Locale ?? "fr"}-{region}";

            // ✅ Disponibilité — fait système, un seul serveur pour toute l'instance.
            var serverMovie = await _db.ServerMovie
                .FirstOrDefaultAsync(sm => sm.MovieId == movie.Id);

            var isAvailable = serverMovie is not null;

            string? jellyfinUrl = null;
            string? plexUrl = null;

            if (serverMovie is not null)
            {
                var serverConfig = await _db.ServerConfig.FirstOrDefaultAsync();
                if (serverConfig is not null)
                {
                    var url = ExternalLinkHelper.BuildDeepLink(
                        serverConfig.Type, _encryption.Decrypt(serverConfig.UrlEncrypted),
                        serverConfig.MachineIdentifier, serverMovie.ServerItemId);

                    if (serverConfig.Type == ServerType.Jellyfin) jellyfinUrl = url;
                    else plexUrl = url;
                }
            }

            var request = await _db.MediaRequests
                .Where(r => r.MovieId == movie.Id && r.UserId == CurrentUserId
                         && r.Status != RequestStatus.Declined)
                .Select(r => new { r.Id, r.Status })
                .FirstOrDefaultAsync();

            var detail = await _tmdb.GetMovieDetailsAsync(movie.TmdbId, locale, region);

            // ✅ Si TMDB est injoignable, on retombe sur le cache local
            // (moins frais / non localisé, mais mieux que rien)
            if (detail is null)
                return Ok(new
                {
                    movie.Id,
                    movie.TmdbId,
                    movie.Title,
                    movie.OriginalTitle,
                    movie.PosterPath,
                    movie.BackdropPath,
                    movie.Overview,
                    Certification = (string?)null,
                    movie.TmdbRating,
                    movie.TmdbPopularity,
                    movie.RuntimeMinutes,
                    movie.Genres,
                    movie.Directors,
                    CastTop10 = movie.CastTop5,
                    movie.ReleaseDate,
                    movie.ContentType,
                    IsAvailable = isAvailable,
                    JellyfinUrl = jellyfinUrl,
                    PlexUrl = plexUrl,
                    RequestId = request?.Id,
                    RequestStatus = request?.Status,
                });

            return Ok(new
            {
                movie.Id,
                detail.TmdbId,
                detail.Title,
                detail.OriginalTitle,
                detail.PosterPath,
                detail.BackdropPath,
                detail.Overview,
                detail.Certification,
                detail.TmdbRating,
                detail.TmdbPopularity,
                detail.RuntimeMinutes,
                detail.Genres,
                detail.Directors,
                detail.CastTop10,
                detail.ReleaseDate,
                movie.ContentType,
                IsAvailable = isAvailable,
                JellyfinUrl = jellyfinUrl,
                PlexUrl = plexUrl,
                RequestId = request?.Id,
                RequestStatus = request?.Status,
            });
        }

        /// <summary>
        /// Clé YouTube de la bande-annonce — appelé uniquement au survol
        /// prolongé d'une carte, jamais en masse. Mis en cache côté backend.
        /// </summary>
        [HttpGet("{id}/trailer")]
        public async Task<IActionResult> GetTrailer(Guid id)
        {
            var movie = await _db.Movies.FindAsync(id);
            if (movie is null) return NotFound();

            var youtubeKey = await _tmdb.GetMovieTrailerAsync(movie.TmdbId);
            return Ok(new { youtubeKey });
        }
    }
}