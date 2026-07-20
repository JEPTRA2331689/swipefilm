// swipefilm/Controllers/MediaRequestController.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using swipefilm.Auth;
using swipefilm.Models;

namespace swipefilm.Controllers
{
    [ApiController]
    [Route("api/requests")]
    [Authorize]
    public class MediaRequestController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly MediaRequestService _requestService;

        public MediaRequestController(
            AppDbContext db,
            MediaRequestService requestService)
        {
            _db = db;
            _requestService = requestService;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private long CurrentUserPermissions =>
            AuthManager.GetPermissions(User);

        // ─── GET /api/requests ────────────────────────────────────────
        // Liste les requêtes — filtrées selon les permissions

        [HttpGet]
        [RequirePermission(Permission.CanRequest)]
        public async Task<IActionResult> GetRequests(
            [FromQuery] RequestStatus? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var canViewAll = PermissionHelper.HasPermission(
                CurrentUserPermissions, Permission.ViewRequests);

            var query = _db.MediaRequests
                .Include(r => r.Movie)
                .Include(r => r.Series)
                .Include(r => r.Seasons)
                .Include(r => r.User)
                .Include(r => r.ProcessedBy)
                .AsNoTracking();

            // ✅ User normal voit seulement ses propres requêtes
            if (!canViewAll)
                query = query.Where(r => r.UserId == CurrentUserId);

            // ✅ Filtre par statut si demandé
            if (status.HasValue)
                query = query.Where(r => r.Status == status.Value);

            var total = await query.CountAsync();

            var requests = await query
                .OrderByDescending(r => r.RequestedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                Total = total,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(total / (double)pageSize),
                Results = requests.Select(MapToDto)
            });
        }

        // ─── GET /api/requests/count ──────────────────────────────────
        // Compteur pour le badge dans la nav

        [HttpGet("count")]
        [RequirePermission(Permission.CanRequest)]
        public async Task<IActionResult> GetCount()
        {
            var canViewAll = PermissionHelper.HasPermission(
                CurrentUserPermissions, Permission.ViewRequests);

            // ✅ Admin voit le nb de requêtes en attente de tous les users
            // User normal voit seulement ses propres requêtes en attente
            var pendingQuery = _db.MediaRequests
                .Where(r => r.Status == RequestStatus.Pending);

            if (!canViewAll)
                pendingQuery = pendingQuery.Where(r => r.UserId == CurrentUserId);

            var pending = await pendingQuery.CountAsync();
            var myTotal = await _db.MediaRequests
                .CountAsync(r => r.UserId == CurrentUserId);
            var myPending = await _db.MediaRequests
                .CountAsync(r => r.UserId == CurrentUserId
                              && r.Status == RequestStatus.Pending);

            return Ok(new
            {
                Pending = pending,    // badge admin — toutes les requêtes en attente
                MyTotal = myTotal,    // toutes mes requêtes
                MyPending = myPending   // mes requêtes en attente
            });
        }

        // ─── GET /api/requests/{id} ───────────────────────────────────

        [HttpGet("{id:guid}")]
        [RequirePermission(Permission.CanRequest)]
        public async Task<IActionResult> GetRequest(Guid id)
        {
            var request = await _db.MediaRequests
                .Include(r => r.Movie)
                .Include(r => r.Series)
                .Include(r => r.Seasons)
                .Include(r => r.User)
                .Include(r => r.ProcessedBy)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request is null) return NotFound();

            // ✅ User normal ne peut voir que ses propres requêtes
            var canViewAll = PermissionHelper.HasPermission(
                CurrentUserPermissions, Permission.ViewRequests);

            if (!canViewAll && request.UserId != CurrentUserId)
                return StatusCode(403, new { error = "Accès refusé" });

            return Ok(MapToDto(request));
        }

        // ─── POST /api/requests ───────────────────────────────────────
        // Créer une requête

        [HttpPost]
        [RequirePermission(Permission.CanRequest)]
        public async Task<IActionResult> CreateRequest(
            [FromBody] CreateRequestDto dto)
        {
            if ((dto.MovieId is null) == (dto.SeriesId is null))
                return BadRequest(new { error = "Il faut renseigner soit MovieId soit SeriesId, jamais les deux ni aucun" });

            var userPerms = CurrentUserPermissions;
            var autoApprove = PermissionHelper.HasPermission(
                userPerms, Permission.AutoApprove);

            var type = dto.MovieId.HasValue ? MediaRequestType.Movie : MediaRequestType.Tv;

            if (dto.MovieId.HasValue && await _db.Movies.FindAsync(dto.MovieId.Value) is null)
                return NotFound(new { error = "Film introuvable" });
            if (dto.SeriesId.HasValue && await _db.Series.FindAsync(dto.SeriesId.Value) is null)
                return NotFound(new { error = "Série introuvable" });

            // ✅ Vérifie le quota
            var (allowed, reason) = await _requestService.CheckQuotaAsync(
                CurrentUserId, type);

            if (!allowed)
                return StatusCode(429, new { error = reason });

            try
            {
                var (request, wasAutoApproved) = await _requestService.CreateRequestAsync(
                    CurrentUserId, dto.MovieId, dto.SeriesId, dto.Message, autoApprove,
                    dto.SeasonNumbers, dto.QualityProfileId, dto.RootFolderPath);

                return Ok(new
                {
                    Request = MapToDto(request),
                    AutoApproved = wasAutoApproved,
                    Message = wasAutoApproved
                        ? $"Ajouté directement à {(type == MediaRequestType.Movie ? "Radarr" : "Sonarr")}"
                        : "Requête envoyée — en attente d'approbation"
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // ─── PUT /api/requests/{id}/approve ──────────────────────────

        [HttpPut("{id:guid}/approve")]
        [RequirePermission(Permission.ManageRequests)]
        public async Task<IActionResult> Approve(Guid id)
        {
            try
            {
                var request = await _requestService.ApproveAsync(id, CurrentUserId);
                return Ok(new
                {
                    Request = MapToDto(request),
                    Message = "Requête approuvée — envoi à Radarr/Sonarr en cours"
                });
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // ─── PUT /api/requests/{id}/decline ──────────────────────────

        [HttpPut("{id:guid}/decline")]
        [RequirePermission(Permission.ManageRequests)]
        public async Task<IActionResult> Decline(
            Guid id, [FromBody] DeclineRequestDto dto)
        {
            try
            {
                var request = await _requestService.DeclineAsync(
                    id, CurrentUserId, dto.Reason);

                return Ok(new
                {
                    Request = MapToDto(request),
                    Message = "Requête refusée"
                });
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // ─── DELETE /api/requests/{id} ────────────────────────────────
        // Annuler sa propre requête

        [HttpDelete("{id:guid}")]
        [RequirePermission(Permission.CanRequest)]
        public async Task<IActionResult> Cancel(Guid id)
        {
            var request = await _db.MediaRequests
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request is null) return NotFound();

            // ✅ Seul le créateur ou un admin peut annuler
            var canManage = PermissionHelper.HasPermission(
                CurrentUserPermissions, Permission.ManageRequests);

            if (request.UserId != CurrentUserId && !canManage)
                return StatusCode(403, new { error = "Accès refusé" });

            if (request.Status != RequestStatus.Pending)
                return BadRequest(new
                {
                    error = "Seules les requêtes en attente peuvent être annulées"
                });

            _db.MediaRequests.Remove(request);
            await _db.SaveChangesAsync();

            return Ok(new { Message = "Requête annulée" });
        }

        // ─── GET /api/requests/movie/{tmdbId} ────────────────────────
        // Vérifie si un film a une requête en cours — utile pour l'UI

        [HttpGet("movie/{tmdbId:int}")]
        [RequirePermission(Permission.CanRequest)]
        public async Task<IActionResult> GetMovieRequestStatus(int tmdbId)
        {
            var movie = await _db.Movies
                .FirstOrDefaultAsync(m => m.TmdbId == tmdbId);

            if (movie is null)
                return Ok(new { HasRequest = false, Request = (object?)null });

            var myRequest = await _db.MediaRequests
                .Include(r => r.User)
                .Where(r => r.MovieId == movie.Id
                         && r.UserId == CurrentUserId
                         && r.Status != RequestStatus.Declined)
                .FirstOrDefaultAsync();

            // ✅ Admin voit aussi si quelqu'un d'autre a déjà demandé
            var canViewAll = PermissionHelper.HasPermission(
                CurrentUserPermissions, Permission.ViewRequests);

            MediaRequest? anyRequest = null;
            if (canViewAll && myRequest is null)
                anyRequest = await _db.MediaRequests
                    .Include(r => r.User)
                    .Where(r => r.MovieId == movie.Id
                             && r.Status != RequestStatus.Declined)
                    .FirstOrDefaultAsync();

            var request = myRequest ?? anyRequest;

            return Ok(new
            {
                HasRequest = request is not null,
                IsMyRequest = myRequest is not null,
                Request = request is null ? null : MapToDto(request)
            });
        }

        // ─── GET /api/requests/series/{tmdbId} ───────────────────────
        // Vérifie si une série a une requête en cours — utile pour l'UI

        [HttpGet("series/{tmdbId:int}")]
        [RequirePermission(Permission.CanRequest)]
        public async Task<IActionResult> GetSeriesRequestStatus(int tmdbId)
        {
            var series = await _db.Series
                .FirstOrDefaultAsync(s => s.TmdbId == tmdbId);

            if (series is null)
                return Ok(new { HasRequest = false, Request = (object?)null });

            var myRequest = await _db.MediaRequests
                .Include(r => r.User)
                .Where(r => r.SeriesId == series.Id
                         && r.UserId == CurrentUserId
                         && r.Status != RequestStatus.Declined)
                .FirstOrDefaultAsync();

            var canViewAll = PermissionHelper.HasPermission(
                CurrentUserPermissions, Permission.ViewRequests);

            MediaRequest? anyRequest = null;
            if (canViewAll && myRequest is null)
                anyRequest = await _db.MediaRequests
                    .Include(r => r.User)
                    .Where(r => r.SeriesId == series.Id
                             && r.Status != RequestStatus.Declined)
                    .FirstOrDefaultAsync();

            var request = myRequest ?? anyRequest;

            return Ok(new
            {
                HasRequest = request is not null,
                IsMyRequest = myRequest is not null,
                Request = request is null ? null : MapToDto(request)
            });
        }

        // ─── Helper ───────────────────────────────────────────────────

        private static object MapToDto(MediaRequest r) => new
        {
            r.Id,
            r.Status,
            r.Type,
            r.RequestedAt,
            r.ProcessedAt,
            r.AvailableAt,
            r.DeclineReason,
            r.Message,
            r.ExternalId,
            r.QualityProfileId,
            r.RootFolderPath,
            SeasonNumbers = r.Seasons.Select(s => s.SeasonNumber).OrderBy(n => n).ToList(),
            Movie = r.Movie is null ? null : new
            {
                r.Movie.Id,
                r.Movie.TmdbId,
                r.Movie.Title,
                r.Movie.PosterPath,
                r.Movie.TmdbRating,
                r.Movie.ContentType
            },
            Series = r.Series is null ? null : new
            {
                r.Series.Id,
                r.Series.TmdbId,
                r.Series.Title,
                r.Series.PosterPath,
                r.Series.TmdbRating
            },
            RequestedBy = r.User is null ? null : new
            {
                r.User.Id,
                r.User.DisplayName,
                r.User.AvatarUrl
            },
            ProcessedBy = r.ProcessedBy is null ? null : new
            {
                r.ProcessedBy.Id,
                r.ProcessedBy.DisplayName
            }
        };
    }

    public record CreateRequestDto(
        Guid? MovieId,
        Guid? SeriesId,
        string? Message,
        List<int>? SeasonNumbers = null,       // Type == Tv uniquement, vide = toute la série
        int? QualityProfileId = null,          // sinon préférence admin par défaut
        string? RootFolderPath = null
    );

    public record DeclineRequestDto(string? Reason);
}