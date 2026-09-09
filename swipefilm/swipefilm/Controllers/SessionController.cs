using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using swipefilm.Auth;
using swipefilm.Data;
using swipefilm.Hubs;
using swipefilm.Models;

namespace swipefilm.Controllers
{
    [ApiController]
    [Route("api/sessions")]
    [Authorize]
    public class SessionController : ControllerBase
    {
        // ✅ Seuil avant de basculer le pool sur le profil de groupe live plutôt
        // que la seule union des suggestions initiales — même principe que
        // AUTO_UPDATE_AFTER=3 déjà utilisé côté swipe solo (swipe/page.tsx).
        private const int RefinementThreshold = 3;

        private readonly AppDbContext _db;
        private readonly SessionService _sessions;
        private readonly IHubContext<SessionHub> _hub;

        public SessionController(AppDbContext db, SessionService sessions, IHubContext<SessionHub> hub)
        {
            _db = db;
            _sessions = sessions;
            _hub = hub;
        }

        private Guid CurrentUserId =>
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // ─── Créer ──────────────────────────────────────────────────────

        [HttpPost]
        public async Task<IActionResult> Create()
        {
            var session = await _sessions.CreateSessionAsync(CurrentUserId);
            var state = await _sessions.GetSessionStateAsync(session.Id);
            return Ok(state);
        }

        // ─── État ───────────────────────────────────────────────────────
        // ✅ Anonyme — /join/{code} doit pouvoir vérifier qu'un code existe
        // avant même que l'utilisateur choisisse connexion ou invité.

        [HttpGet("{code}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetState(string code)
        {
            var session = await _sessions.GetViewableSessionByCodeAsync(code);
            if (session is null) return NotFound(new { error = "Session introuvable ou expirée" });

            var state = await _sessions.GetSessionStateAsync(session.Id);
            return Ok(state);
        }

        // ─── Rejoindre ──────────────────────────────────────────────────

        [HttpPost("join")]
        [AllowAnonymous]
        public async Task<IActionResult> Join([FromBody] JoinSessionDto dto)
        {
            try
            {
                if (User.Identity?.IsAuthenticated == true)
                {
                    var session = await _sessions.JoinAsync(dto.Code, CurrentUserId);
                    return Ok(await _sessions.GetSessionStateAsync(session.Id));
                }

                if (string.IsNullOrWhiteSpace(dto.DisplayName))
                    return BadRequest(new { error = "Un prénom est requis pour rejoindre en invité" });

                var (guestSession, _) = await _sessions.JoinAsGuestAsync(HttpContext, dto.Code, dto.DisplayName);
                return Ok(await _sessions.GetSessionStateAsync(guestSession.Id));
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { error = ex.Message });
            }
        }

        // ─── Lancement — hôte uniquement ────────────────────────────────
        // Quitte l'écran de lobby (QR/code) pour l'écran de configuration.

        [HttpPost("{code}/launch")]
        public async Task<IActionResult> Launch(string code)
        {
            var session = await _sessions.GetActiveSessionByCodeAsync(code);
            if (session is null) return NotFound(new { error = "Session introuvable ou expirée" });

            try
            {
                await _sessions.LaunchSessionAsync(session.Id, CurrentUserId);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { error = ex.Message });
            }

            var state = await _sessions.GetSessionStateAsync(session.Id);
            await _hub.Clients.Group(session.Code).SendAsync("PhaseChanged", state);
            return Ok(state);
        }

        // ─── Genre — hôte uniquement ───────────────────────────────────

        [HttpPost("{code}/genre")]
        public async Task<IActionResult> SetGenre(string code, [FromBody] SetGenreDto dto)
        {
            var session = await _sessions.GetActiveSessionByCodeAsync(code);
            if (session is null) return NotFound(new { error = "Session introuvable ou expirée" });

            try
            {
                await _sessions.SetGenreFilterAsync(session.Id, CurrentUserId, dto.Genres, dto.ContentType);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { error = ex.Message });
            }

            var state = await _sessions.GetSessionStateAsync(session.Id);
            await _hub.Clients.Group(session.Code).SendAsync("PhaseChanged", state);
            return Ok(state);
        }

        // ─── Démarrage — hôte uniquement ────────────────────────────────
        // Dernière étape de l'onboarding hôte (après le genre) — fixe la
        // durée de la session et bascule en phase swipe.

        [HttpPost("{code}/start")]
        public async Task<IActionResult> Start(string code, [FromBody] StartSessionDto dto)
        {
            var session = await _sessions.GetActiveSessionByCodeAsync(code);
            if (session is null) return NotFound(new { error = "Session introuvable ou expirée" });

            try
            {
                await _sessions.StartSessionAsync(session.Id, CurrentUserId, dto.DurationMinutes);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { error = ex.Message });
            }

            var state = await _sessions.GetSessionStateAsync(session.Id);
            await _hub.Clients.Group(session.Code).SendAsync("PhaseChanged", state);
            return Ok(state);
        }

        // ─── Pool commun ────────────────────────────────────────────────
        // Bascule automatiquement du premier jet (union des suggestions par
        // membre) au deuxième (profil de groupe live) une fois le seuil de
        // swipes par membre atteint.

        [HttpGet("{code}/pool")]
        public async Task<IActionResult> GetPool(string code, [FromQuery] int count = 40)
        {
            var session = await _sessions.GetActiveSessionByCodeAsync(code);
            if (session is null) return NotFound(new { error = "Session introuvable ou expirée" });

            var memberCount = await _db.SessionMembers.CountAsync(m => m.SessionId == session.Id);
            var swipeCount = await _db.Swipes.CountAsync(s => s.SessionId == session.Id);
            var avgSwipesPerMember = memberCount > 0 ? swipeCount / (float)memberCount : 0f;

            var pool = avgSwipesPerMember >= RefinementThreshold
                ? await _sessions.RefinePoolWithGroupProfileAsync(session.Id, count)
                : await _sessions.BuildInitialPoolAsync(session.Id, count);

            return Ok(new { pool, refined = avgSwipesPerMember >= RefinementThreshold });
        }

        // ─── Swipe ──────────────────────────────────────────────────────

        [HttpPost("{code}/swipe")]
        public async Task<IActionResult> Swipe(string code, [FromBody] SessionSwipeDto dto)
        {
            var session = await _sessions.GetActiveSessionByCodeAsync(code);
            if (session is null) return NotFound(new { error = "Session introuvable ou expirée" });

            try
            {
                await _sessions.RecordSwipeAsync(
                    session.Id, CurrentUserId, dto.MovieId, dto.SeriesId, dto.Direction, dto.DurationMs);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }

            var count = await _db.Swipes.CountAsync(s => s.SessionId == session.Id && s.UserId == CurrentUserId);
            await _hub.Clients.Group(session.Code).SendAsync(
                "SwipeProgress", new { memberId = CurrentUserId, count });

            return Ok(new { message = "Swipe enregistré" });
        }

        // ─── Résultats ──────────────────────────────────────────────────

        [HttpGet("{code}/matches")]
        public async Task<IActionResult> GetMatches(string code)
        {
            var session = await _sessions.GetViewableSessionByCodeAsync(code);
            if (session is null) return NotFound(new { error = "Session introuvable ou expirée" });

            var matches = await _sessions.ComputeMatchesAsync(session.Id);
            return Ok(matches);
        }

        // ─── Révélation — hôte uniquement ───────────────────────────────

        [HttpPost("{code}/reveal")]
        public async Task<IActionResult> Reveal(string code)
        {
            var session = await _sessions.GetActiveSessionByCodeAsync(code);
            if (session is null) return NotFound(new { error = "Session introuvable ou expirée" });
            if (session.CreatedByUserId != CurrentUserId)
                return StatusCode(403, new { error = "Seul l'hôte peut révéler les résultats" });

            var matches = await _sessions.ComputeMatchesAsync(session.Id);
            await _sessions.CompleteSessionAsync(session.Id);

            await _hub.Clients.Group(session.Code).SendAsync("MatchesRevealed", matches);
            return Ok(matches);
        }
    }

    // ─── DTOs ─────────────────────────────────────────────────────────────

    public record JoinSessionDto(string Code, string? DisplayName);

    public record SetGenreDto(string[]? Genres, HomeContentFilter ContentType);

    public record StartSessionDto(int? DurationMinutes);

    public record SessionSwipeDto(
        Guid? MovieId, Guid? SeriesId, SwipeDirection Direction, int DurationMs);
}
