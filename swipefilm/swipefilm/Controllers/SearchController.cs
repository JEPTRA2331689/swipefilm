// swipefilm/Controllers/SearchController.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using swipefilm.Auth.swipefilm.Services;
using swipefilm.Data;
using swipefilm.Models;

namespace swipefilm.Controllers
{
    /// <summary>
    /// Recherche utilisateur — /search/multi TMDB en direct, avec upsert
    /// paresseux des films/séries pas encore connus localement. Fait en LOT
    /// (une requête SELECT + un SaveChanges pour toute la page de résultats,
    /// pas un aller-retour par résultat) — le round-trip vers TMDB domine de
    /// toute façon le temps de réponse, pas la peine de payer un N+1 en plus.
    /// </summary>
    [ApiController]
    [Route("api/search")]
    [Authorize]
    public class SearchController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly TmdbService _tmdb;

        public SearchController(AppDbContext db, TmdbService tmdb)
        {
            _db = db;
            _tmdb = tmdb;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public record SearchResultItemDto(
            Guid Id,
            int TmdbId,
            string Title,
            string? PosterPath,
            DateOnly? ReleaseDate,
            float TmdbRating,
            string ContentType,
            bool IsAvailable
        );

        [HttpGet]
        public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int page = 1)
        {
            if (string.IsNullOrWhiteSpace(q))
                return Ok(new { results = Array.Empty<SearchResultItemDto>(), page, totalPages = 0 });

            var user = await _db.Users.FindAsync(CurrentUserId);
            var region = user?.Region ?? "FR";
            var locale = $"{user?.Locale ?? "fr"}-{region}";

            var searchResult = await _tmdb.SearchMultiAsync(q, locale, page);
            if (searchResult is null || searchResult.Results.Count == 0)
                return Ok(new { results = Array.Empty<SearchResultItemDto>(), page, totalPages = 0 });

            var movieItems = searchResult.Results.Where(r => r.MediaType == "movie").ToList();
            var seriesItems = searchResult.Results.Where(r => r.MediaType == "tv").ToList();

            // ✅ Films — une seule lecture, un seul insert pour tous les
            // tmdbIds pas encore connus localement
            var movieTmdbIds = movieItems.Select(r => r.TmdbId).ToList();
            var movieIdByTmdbId = await _db.Movies
                .Where(m => movieTmdbIds.Contains(m.TmdbId))
                .ToDictionaryAsync(m => m.TmdbId, m => m.Id);

            var newMovies = new List<Movie>();
            foreach (var item in movieItems)
            {
                if (movieIdByTmdbId.ContainsKey(item.TmdbId)) continue;

                var movie = new Movie
                {
                    Id = Guid.NewGuid(),
                    TmdbId = item.TmdbId,
                    Title = item.Title,
                    PosterPath = item.PosterPath,
                    Overview = item.Overview,
                    ReleaseDate = item.ReleaseDate,
                    TmdbRating = item.TmdbRating,
                    ContentType = "movie",
                    CachedAt = DateTime.MinValue, // ✅ repris par le job d'enrichissement quotidien
                };
                newMovies.Add(movie);
                movieIdByTmdbId[item.TmdbId] = movie.Id;
            }

            // ✅ Séries — même principe
            var seriesTmdbIds = seriesItems.Select(r => r.TmdbId).ToList();
            var seriesIdByTmdbId = await _db.Series
                .Where(s => seriesTmdbIds.Contains(s.TmdbId))
                .ToDictionaryAsync(s => s.TmdbId, s => s.Id);

            var newSeries = new List<Series>();
            foreach (var item in seriesItems)
            {
                if (seriesIdByTmdbId.ContainsKey(item.TmdbId)) continue;

                var series = new Series
                {
                    Id = Guid.NewGuid(),
                    TmdbId = item.TmdbId,
                    Title = item.Title,
                    PosterPath = item.PosterPath,
                    Overview = item.Overview,
                    FirstAirDate = item.ReleaseDate,
                    TmdbRating = item.TmdbRating,
                    CachedAt = DateTime.MinValue,
                };
                newSeries.Add(series);
                seriesIdByTmdbId[item.TmdbId] = series.Id;
            }

            if (newMovies.Count > 0) _db.Movies.AddRange(newMovies);
            if (newSeries.Count > 0) _db.Series.AddRange(newSeries);
            if (newMovies.Count > 0 || newSeries.Count > 0) await _db.SaveChangesAsync();

            // ✅ Disponibilité — un seul aller-retour par type, pas par résultat
            var allMovieIds = movieIdByTmdbId.Values.ToList();
            var availableMovieIds = (await _db.ServerMovie
                .Where(sm => allMovieIds.Contains(sm.MovieId))
                .Select(sm => sm.MovieId)
                .ToListAsync())
                .ToHashSet();

            var allSeriesIds = seriesIdByTmdbId.Values.ToList();
            var availableSeriesIds = (await _db.ServerSeries
                .Where(ss => allSeriesIds.Contains(ss.SeriesId))
                .Select(ss => ss.SeriesId)
                .ToListAsync())
                .ToHashSet();

            var results = searchResult.Results.Select(item =>
            {
                var isMovie = item.MediaType == "movie";
                var id = isMovie ? movieIdByTmdbId[item.TmdbId] : seriesIdByTmdbId[item.TmdbId];
                var isAvailable = isMovie ? availableMovieIds.Contains(id) : availableSeriesIds.Contains(id);

                return new SearchResultItemDto(
                    Id: id,
                    TmdbId: item.TmdbId,
                    Title: item.Title,
                    PosterPath: item.PosterPath,
                    ReleaseDate: item.ReleaseDate,
                    TmdbRating: item.TmdbRating,
                    ContentType: isMovie ? "movie" : "series",
                    IsAvailable: isAvailable
                );
            }).ToList();

            return Ok(new { results, page, totalPages = searchResult.TotalPages });
        }
    }
}
