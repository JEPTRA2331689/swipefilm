// swipefilm/Controllers/SeriesController.cs
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
    [Route("api/series")]
    [Authorize]
    public class SeriesController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly TmdbService _tmdb;
        private readonly IEncryptionService _encryption;

        public SeriesController(AppDbContext db, TmdbService tmdb, IEncryptionService encryption)
        {
            _db = db;
            _tmdb = tmdb;
            _encryption = encryption;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>
        /// Détail d'une série — mêmes principes que MovieController.GetMovie :
        /// champs sensibles à la région/langue récupérés en direct depuis TMDB,
        /// jamais mis en cache en base, plus disponibilité et statut de demande.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetSeries(Guid id)
        {
            var series = await _db.Series.FindAsync(id);
            if (series is null) return NotFound();

            var user = await _db.Users.FindAsync(CurrentUserId);
            var region = user?.Region ?? "FR";
            var locale = $"{user?.Locale ?? "fr"}-{region}";

            var detail = await _tmdb.GetSeriesDetailsAsync(series.TmdbId, locale, region);

            // ✅ "Disponible" = TOUTES les saisons réelles (TMDB) sont possédées —
            // pas juste "la série a une entrée dans la bibliothèque" (Jellyfin la
            // crée dès la première saison présente, même si la série est
            // incomplète). Sans ça, le bouton Requêter reste caché alors qu'il
            // manque des saisons. Même logique que GetSeasons.
            var localSeasons = await _db.SeriesSeasons
                .Where(s => s.SeriesId == series.Id)
                .ToDictionaryAsync(s => s.SeasonNumber, s => s.Id);

            var localSeasonIds = localSeasons.Values.ToList();
            var ownedSeasonIds = (await _db.ServerSeriesSeasons
                .Where(sss => localSeasonIds.Contains(sss.SeriesSeasonId))
                .Select(sss => sss.SeriesSeasonId)
                .ToListAsync())
                .ToHashSet();

            var realSeasonNumbers = detail is not null
                ? detail.Seasons.Select(s => s.SeasonNumber).ToList()
                : localSeasons.Keys.ToList(); // repli si TMDB injoignable

            var isAvailable = realSeasonNumbers.Count > 0 && realSeasonNumbers.All(n =>
                localSeasons.TryGetValue(n, out var seasonId) && ownedSeasonIds.Contains(seasonId));

            // ✅ Lien direct Jellyfin/Plex — dès que la série a une entrée sur
            // le serveur (même incomplète), pas seulement si isAvailable=true.
            // Fait système, un seul serveur pour toute l'instance.
            var serverSeries = await _db.ServerSeries
                .FirstOrDefaultAsync(ss => ss.SeriesId == series.Id);

            string? jellyfinUrl = null;
            string? plexUrl = null;

            if (serverSeries is not null)
            {
                var serverConfig = await _db.ServerConfig.FirstOrDefaultAsync();
                if (serverConfig is not null)
                {
                    var url = ExternalLinkHelper.BuildDeepLink(
                        serverConfig.Type, _encryption.Decrypt(serverConfig.UrlEncrypted),
                        serverConfig.MachineIdentifier, serverSeries.ServerItemId);

                    if (serverConfig.Type == ServerType.Jellyfin) jellyfinUrl = url;
                    else plexUrl = url;
                }
            }

            var request = await _db.MediaRequests
                .Where(r => r.SeriesId == series.Id && r.UserId == CurrentUserId
                         && r.Status != RequestStatus.Declined)
                .Select(r => new { r.Id, r.Status })
                .FirstOrDefaultAsync();

            if (detail is null)
                return Ok(new
                {
                    series.Id,
                    series.TmdbId,
                    series.Title,
                    series.OriginalTitle,
                    series.PosterPath,
                    series.BackdropPath,
                    series.Overview,
                    Certification = (string?)null,
                    series.TmdbRating,
                    series.TmdbPopularity,
                    series.NumberOfSeasons,
                    series.NumberOfEpisodes,
                    series.Genres,
                    series.CreatedBy,
                    CastTop10 = series.CastTop5,
                    series.FirstAirDate,
                    ContentType = "series",
                    IsAvailable = isAvailable,
                    JellyfinUrl = jellyfinUrl,
                    PlexUrl = plexUrl,
                    RequestId = request?.Id,
                    RequestStatus = request?.Status,
                });

            return Ok(new
            {
                series.Id,
                detail.TmdbId,
                detail.Title,
                detail.OriginalTitle,
                detail.PosterPath,
                detail.BackdropPath,
                detail.Overview,
                detail.Certification,
                detail.TmdbRating,
                detail.TmdbPopularity,
                detail.NumberOfSeasons,
                detail.NumberOfEpisodes,
                detail.Genres,
                detail.CreatedBy,
                detail.CastTop10,
                detail.FirstAirDate,
                ContentType = "series",
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
            var series = await _db.Series.FindAsync(id);
            if (series is null) return NotFound();

            var youtubeKey = await _tmdb.GetSeriesTrailerAsync(series.TmdbId);
            return Ok(new { youtubeKey });
        }

        /// <summary>
        /// Détail par saison — la liste des saisons vient de TMDB en direct (pas
        /// de la table locale SeriesSeasons, qui n'est peuplée qu'au fil des sync
        /// Jellyfin/Plex et ne contient donc que les saisons déjà possédées —
        /// sinon impossible de voir/requêter une saison qu'on n'a pas).
        /// Disponibilité (ServerSeriesSeason) et statut de demande croisés par
        /// numéro de saison (une demande "série entière" couvre toutes les
        /// saisons, une demande ciblée ne couvre que celles dans MediaRequestSeason).
        /// </summary>
        [HttpGet("{id}/seasons")]
        public async Task<IActionResult> GetSeasons(Guid id)
        {
            var series = await _db.Series.FindAsync(id);
            if (series is null) return NotFound();

            var localSeasons = await _db.SeriesSeasons
                .Where(s => s.SeriesId == id)
                .ToDictionaryAsync(s => s.SeasonNumber, s => s);

            var user = await _db.Users.FindAsync(CurrentUserId);
            var region = user?.Region ?? "FR";
            var locale = $"{user?.Locale ?? "fr"}-{region}";
            var detail = await _tmdb.GetSeriesDetailsAsync(series.TmdbId, locale, region);

            // (numéro de saison, nombre d'épisodes, poster) — TMDB si joignable,
            // sinon repli sur la table locale (seasons possédées uniquement,
            // pas de poster stocké localement)
            var seasonList = detail is not null
                ? detail.Seasons.Select(s => (s.SeasonNumber, s.EpisodeCount, s.PosterPath)).ToList()
                : localSeasons.Values
                    .OrderBy(s => s.SeasonNumber)
                    .Select(s => (s.SeasonNumber, s.EpisodeCount, PosterPath: (string?)null))
                    .ToList();

            var localSeasonIds = localSeasons.Values.Select(s => s.Id).ToList();
            var availableSeasonIds = (await _db.ServerSeriesSeasons
                .Where(sss => localSeasonIds.Contains(sss.SeriesSeasonId))
                .Select(sss => sss.SeriesSeasonId)
                .ToListAsync())
                .ToHashSet();

            var myRequests = await _db.MediaRequests
                .Include(r => r.Seasons)
                .Where(r => r.SeriesId == id && r.UserId == CurrentUserId
                         && r.Status != RequestStatus.Declined)
                .ToListAsync();

            var wholeSeriesRequest = myRequests.FirstOrDefault(r => !r.Seasons.Any());

            var result = seasonList.Select(season =>
            {
                var isAvailable = localSeasons.TryGetValue(season.SeasonNumber, out var localSeason)
                    && availableSeasonIds.Contains(localSeason.Id);

                var seasonRequest = wholeSeriesRequest
                    ?? myRequests.FirstOrDefault(r => r.Seasons.Any(s => s.SeasonNumber == season.SeasonNumber));

                return new
                {
                    season.SeasonNumber,
                    season.EpisodeCount,
                    season.PosterPath,
                    IsAvailable = isAvailable,
                    RequestId = seasonRequest?.Id,
                    RequestStatus = seasonRequest?.Status,
                };
            });

            return Ok(result);
        }
    }
}
