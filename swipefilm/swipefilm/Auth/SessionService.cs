using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using swipefilm.Data;
using swipefilm.Models;

namespace swipefilm.Auth
{
    /// <summary>
    /// Cycle de vie d'une session de swipe en groupe : création, adhésion
    /// (compte réel ou invité éphémère), pool commun en deux jets (cf.
    /// RecommendationEngine.BuildGroupSessionProfilesAsync/BuildGroupSectionAsync)
    /// et calcul des matchs de consensus. Pas de table de phase dédiée — la
    /// phase (lobby / genre choisi / résultats) se déduit de GenreFilter et
    /// Status, cf. GetSessionStateAsync.
    /// </summary>
    public class SessionService
    {
        // Pas de 0/O/1/I — ambiguïté visuelle à l'écran comme au clavier (cf. Kahoot).
        private const string CodeAlphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        private const int CodeLength = 4;

        private readonly AppDbContext _db;
        private readonly RecommendationEngine _engine;
        private readonly UserManager<User> _userManager;
        private readonly AuthManager _authManager;
        private readonly IAppConfigService _config;

        public SessionService(
            AppDbContext db,
            RecommendationEngine engine,
            UserManager<User> userManager,
            AuthManager authManager,
            IAppConfigService config)
        {
            _db = db;
            _engine = engine;
            _userManager = userManager;
            _authManager = authManager;
            _config = config;
        }

        // ─── Création ───────────────────────────────────────────────────

        public async Task<Session> CreateSessionAsync(Guid hostUserId)
        {
            var code = await GenerateUniqueCodeAsync();

            var session = new Session
            {
                Id = Guid.NewGuid(),
                Code = code,
                Status = SessionStatus.Active,
                CreatedByUserId = hostUserId,
                ExpiresAt = DateTime.UtcNow.AddHours(6),
            };
            _db.Sessions.Add(session);
            _db.SessionMembers.Add(new SessionMember
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                UserId = hostUserId,
            });
            await _db.SaveChangesAsync();

            return session;
        }

        private async Task<string> GenerateUniqueCodeAsync()
        {
            var rng = Random.Shared;
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var code = new string(Enumerable.Range(0, CodeLength)
                    .Select(_ => CodeAlphabet[rng.Next(CodeAlphabet.Length)])
                    .ToArray());

                var taken = await _db.Sessions.AnyAsync(s =>
                    s.Code == code && s.Status == SessionStatus.Active);
                if (!taken) return code;
            }
            throw new InvalidOperationException("Impossible de générer un code de session unique");
        }

        // ─── Adhésion ───────────────────────────────────────────────────

        public async Task<Session?> GetActiveSessionByCodeAsync(string code)
        {
            var normalized = code.Trim().ToUpperInvariant();
            return await _db.Sessions.FirstOrDefaultAsync(s =>
                s.Code == normalized
                && s.Status == SessionStatus.Active
                && s.ExpiresAt > DateTime.UtcNow);
        }

        /// <summary>
        /// Rejoindre/swiper/genre/démarrer exigent Active — consulter l'état ou
        /// les résultats reste possible pendant la fenêtre de grâce de 2h après
        /// la fin (CompleteSessionAsync repousse ExpiresAt à ce moment-là).
        /// </summary>
        public async Task<Session?> GetViewableSessionByCodeAsync(string code)
        {
            var normalized = code.Trim().ToUpperInvariant();
            return await _db.Sessions.FirstOrDefaultAsync(s =>
                s.Code == normalized
                && (s.Status == SessionStatus.Active || s.Status == SessionStatus.Completed)
                && s.ExpiresAt > DateTime.UtcNow);
        }

        /// <summary>Rejoint avec un compte déjà connecté (cookie présent).</summary>
        public async Task<Session> JoinAsync(string code, Guid userId)
        {
            var session = await GetActiveSessionByCodeAsync(code)
                ?? throw new InvalidOperationException("Session introuvable ou expirée");

            await AddMemberIfMissingAsync(session.Id, userId);
            return session;
        }

        /// <summary>
        /// Rejoint sans compte : provisionne un vrai User Identity éphémère
        /// (IsGuest=true) et le connecte avec le même mécanisme de cookie que
        /// login/register — aucune identité parallèle à maintenir.
        /// </summary>
        public async Task<(Session Session, User Guest)> JoinAsGuestAsync(
            HttpContext httpContext, string code, string displayName)
        {
            var session = await GetActiveSessionByCodeAsync(code)
                ?? throw new InvalidOperationException("Session introuvable ou expirée");

            var guestId = Guid.NewGuid();
            var email = $"guest-{guestId:N}@swipefilm.local";
            var guest = new User
            {
                Email = email,
                UserName = email,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Invité" : displayName.Trim(),
                CreatedAt = DateTime.UtcNow,
                Permissions = (long)Permission.CanSwipe,
                IsGuest = true,
            };

            var tempPassword = $"{Guid.NewGuid():N}{Guid.NewGuid():N}"[..16] + "Aa1!";
            var result = await _userManager.CreateAsync(guest, tempPassword);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    string.Join(", ", result.Errors.Select(e => e.Description)));

            await AddMemberIfMissingAsync(session.Id, guest.Id);
            await _authManager.SignInAsync(httpContext, guest);

            return (session, guest);
        }

        private async Task AddMemberIfMissingAsync(Guid sessionId, Guid userId)
        {
            var exists = await _db.SessionMembers
                .AnyAsync(m => m.SessionId == sessionId && m.UserId == userId);
            if (exists) return;

            _db.SessionMembers.Add(new SessionMember
            {
                Id = Guid.NewGuid(),
                SessionId = sessionId,
                UserId = userId,
            });
            await _db.SaveChangesAsync();
        }

        // ─── Lancement — hôte seul ──────────────────────────────────────
        // Quitte l'écran de lobby (QR/code, liste des participants) pour
        // l'écran de configuration (genre puis durée) — le QR/code n'a plus
        // de raison d'être affiché une fois cette étape passée.

        public async Task<Session> LaunchSessionAsync(Guid sessionId, Guid requestingUserId)
        {
            var session = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId)
                ?? throw new InvalidOperationException("Session introuvable");

            if (session.CreatedByUserId != requestingUserId)
                throw new UnauthorizedAccessException("Seul l'hôte lance la session");

            session.HasLaunched = true;
            await _db.SaveChangesAsync();

            return session;
        }

        // ─── Choix du genre — hôte seul ───────────────────────────────────

        public async Task<Session> SetGenreFilterAsync(
            Guid sessionId, Guid requestingUserId, string[]? genres, HomeContentFilter contentType)
        {
            var session = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId)
                ?? throw new InvalidOperationException("Session introuvable");

            if (session.CreatedByUserId != requestingUserId)
                throw new UnauthorizedAccessException("Seul l'hôte choisit le genre de la session");

            session.GenreFilter = genres is { Length: > 0 } ? genres : null;
            session.ContentTypeFilter = contentType;
            await _db.SaveChangesAsync();

            return session;
        }

        // ─── Démarrage — hôte seul ─────────────────────────────────────────
        // Étape finale de l'onboarding hôte (après le choix du genre) — fixe
        // la durée de la session (null = pas de changement, garde les 6h par
        // défaut de CreateSessionAsync) et bascule en phase swipe. L'hôte
        // garde la main pour y mettre fin à tout moment via /reveal, quelle
        // que soit la durée choisie ici.

        public async Task<Session> StartSessionAsync(
            Guid sessionId, Guid requestingUserId, int? durationMinutes)
        {
            var session = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId)
                ?? throw new InvalidOperationException("Session introuvable");

            if (session.CreatedByUserId != requestingUserId)
                throw new UnauthorizedAccessException("Seul l'hôte démarre la session");

            if (durationMinutes is > 0)
                session.ExpiresAt = DateTime.UtcNow.AddMinutes(durationMinutes.Value);

            session.HasStarted = true;
            await _db.SaveChangesAsync();

            return session;
        }

        // ─── État ───────────────────────────────────────────────────────

        public async Task<SessionStateDto?> GetSessionStateAsync(Guid sessionId)
        {
            var session = await _db.Sessions
                .Include(s => s.Members).ThenInclude(m => m.User)
                .FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session is null) return null;

            var swipeCounts = await _db.Swipes
                .Where(s => s.SessionId == sessionId)
                .GroupBy(s => s.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.UserId, g => g.Count);

            return new SessionStateDto(
                session.Id,
                session.Code,
                session.Status,
                session.CreatedByUserId,
                session.GenreFilter,
                session.ContentTypeFilter,
                session.HasLaunched,
                session.HasStarted,
                session.Members.Select(m => new SessionMemberDto(
                    m.UserId,
                    m.User.DisplayName,
                    m.User.AvatarUrl,
                    m.User.IsGuest,
                    m.UserId == session.CreatedByUserId,
                    swipeCounts.GetValueOrDefault(m.UserId, 0)
                )).ToList()
            );
        }

        // ─── Pool commun — premier jet ────────────────────────────────────
        // Union des suggestions personnalisées de chaque membre (top-N par
        // membre, entrelacées) — aucun swipe de session n'existe encore.

        public async Task<List<HomeSectionMovie>> BuildInitialPoolAsync(Guid sessionId, int totalCount = 40)
        {
            var session = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId)
                ?? throw new InvalidOperationException("Session introuvable");

            var memberIds = await _db.SessionMembers
                .Where(m => m.SessionId == sessionId)
                .Select(m => m.UserId)
                .ToListAsync();
            if (memberIds.Count == 0) return [];

            var perMember = Math.Max(6, totalCount / memberIds.Count);
            var sectionProfile = BuildGroupSectionProfile(session);
            var wantsMovies = session.ContentTypeFilter != HomeContentFilter.SeriesOnly;
            var wantsSeries = session.ContentTypeFilter != HomeContentFilter.MoviesOnly;

            var perMemberLists = new List<List<HomeSectionMovie>>();
            foreach (var memberId in memberIds)
            {
                var items = new List<HomeSectionMovie>();
                if (wantsMovies)
                {
                    var section = await _engine.GetSectionPageAsync(
                        memberId, sectionProfile, perMember, isSeries: false);
                    if (section != null) items.AddRange(section.Movies);
                }
                if (wantsSeries)
                {
                    var section = await _engine.GetSectionPageAsync(
                        memberId, sectionProfile, perMember, isSeries: true);
                    if (section != null) items.AddRange(section.Movies);
                }
                perMemberLists.Add(items);
            }

            return Interleave(perMemberLists, totalCount);
        }

        // ─── Pool commun — deuxième jet ───────────────────────────────────
        // Remplace le pool par un tirage scoré avec le profil de goût agrégé
        // des swipes réels de la session — converge vers ce que le groupe
        // aime réellement plutôt que ses seules suggestions initiales.

        public async Task<List<HomeSectionMovie>> RefinePoolWithGroupProfileAsync(
            Guid sessionId, int totalCount = 40)
        {
            var session = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId)
                ?? throw new InvalidOperationException("Session introuvable");

            var (movieProfile, seriesProfile) = await _engine.BuildGroupSessionProfilesAsync(sessionId);
            var sectionProfile = BuildGroupSectionProfile(session);

            var excludedMovies = await _db.Swipes
                .Where(s => s.SessionId == sessionId && s.MovieId != null)
                .Select(s => s.Movie!.TmdbId)
                .ToListAsync();
            var excludedSeries = await _db.Swipes
                .Where(s => s.SessionId == sessionId && s.SeriesId != null)
                .Select(s => s.Series!.TmdbId)
                .ToListAsync();

            var wantsMovies = session.ContentTypeFilter != HomeContentFilter.SeriesOnly;
            var wantsSeries = session.ContentTypeFilter != HomeContentFilter.MoviesOnly;
            var half = Math.Max(6, totalCount / 2);

            var results = new List<HomeSectionMovie>();
            if (wantsMovies)
            {
                var section = await _engine.BuildGroupSectionAsync(
                    sectionProfile, wantsSeries ? half : totalCount, isSeries: false,
                    movieProfile, null, excludedMovies.ToHashSet());
                if (section != null) results.AddRange(section.Movies);
            }
            if (wantsSeries)
            {
                var section = await _engine.BuildGroupSectionAsync(
                    sectionProfile, wantsMovies ? half : totalCount, isSeries: true,
                    null, seriesProfile, excludedSeries.ToHashSet());
                if (section != null) results.AddRange(section.Movies);
            }

            return results;
        }

        private static SectionProfile BuildGroupSectionProfile(Session session) => new()
        {
            Id = "group_session",
            Title = "Session de groupe",
            Genres = 0.9f,
            History = 0.5f,
            Actors = 0.3f,
            Directors = 0.3f,
            Rating = 0.4f,
            Language = 0.3f,
            Popularity = 0.15f,
            Randomness = 0.2f,
            FilterByTargetGenres = session.GenreFilter is { Length: > 0 },
            TargetGenres = session.GenreFilter,
            UseBayesianRating = true,
            MinVoteCount = 10,
        };

        private static List<HomeSectionMovie> Interleave(List<List<HomeSectionMovie>> lists, int max)
        {
            var result = new List<HomeSectionMovie>();
            var seen = new HashSet<(string ContentType, Guid Id)>();
            var i = 0;
            var any = true;

            while (result.Count < max && any)
            {
                any = false;
                foreach (var list in lists)
                {
                    if (i >= list.Count) continue;
                    any = true;

                    var item = list[i];
                    if (seen.Add((item.ContentType, item.Id))) result.Add(item);
                    if (result.Count >= max) break;
                }
                i++;
            }

            return result;
        }

        // ─── Swipe ──────────────────────────────────────────────────────

        public async Task RecordSwipeAsync(
            Guid sessionId, Guid userId, Guid? movieId, Guid? seriesId,
            SwipeDirection direction, int durationMs)
        {
            if ((movieId is null) == (seriesId is null))
                throw new InvalidOperationException("Il faut renseigner soit movieId soit seriesId, jamais les deux ni aucun");

            _db.Swipes.Add(new Swipe
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                SessionId = sessionId,
                MovieId = movieId,
                SeriesId = seriesId,
                Direction = direction,
                DurationMs = durationMs,
                ContextMode = SwipeContext.Group,
                CreatedAt = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync();
        }

        // ─── Matchs — consensus, pas unanimité ─────────────────────────────

        public async Task<List<SessionMatchResultDto>> ComputeMatchesAsync(Guid sessionId, int topN = 10)
        {
            var memberCount = await _db.SessionMembers.CountAsync(m => m.SessionId == sessionId);
            if (memberCount == 0) return [];

            var rightSwipes = await _db.Swipes
                .Include(s => s.Movie)
                .Include(s => s.Series)
                .Where(s => s.SessionId == sessionId && s.Direction == SwipeDirection.Right)
                .ToListAsync();

            var byMovie = rightSwipes
                .Where(s => s.MovieId != null)
                .GroupBy(s => s.MovieId!.Value)
                .Select(g => (
                    MovieId: (Guid?)g.Key, SeriesId: (Guid?)null,
                    Title: g.First().Movie!.Title, PosterPath: g.First().Movie!.PosterPath,
                    AgreeCount: g.Select(s => s.UserId).Distinct().Count()))
                .ToList();

            var bySeries = rightSwipes
                .Where(s => s.SeriesId != null)
                .GroupBy(s => s.SeriesId!.Value)
                .Select(g => (
                    MovieId: (Guid?)null, SeriesId: (Guid?)g.Key,
                    Title: g.First().Series!.Title, PosterPath: g.First().Series!.PosterPath,
                    AgreeCount: g.Select(s => s.UserId).Distinct().Count()))
                .ToList();

            var top = byMovie.Concat(bySeries)
                .OrderByDescending(m => m.AgreeCount)
                .ThenBy(_ => Guid.NewGuid())
                .Take(topN)
                .ToList();

            // ✅ Même schéma de disponibilité que la fiche film/série (Movie/
            // SeriesController) : lien direct Jellyfin/Plex si le titre est déjà
            // dans la bibliothèque, sinon on laisse le frontend proposer Requêter.
            var topMovieIds = top.Where(m => m.MovieId != null).Select(m => m.MovieId!.Value).ToList();
            var topSeriesIds = top.Where(m => m.SeriesId != null).Select(m => m.SeriesId!.Value).ToList();

            var availableMovieItems = await _db.ServerMovie
                .Where(sm => topMovieIds.Contains(sm.MovieId))
                .ToDictionaryAsync(sm => sm.MovieId, sm => sm.ServerItemId);
            var availableSeriesItems = await _db.ServerSeries
                .Where(ss => topSeriesIds.Contains(ss.SeriesId))
                .ToDictionaryAsync(ss => ss.SeriesId, ss => ss.ServerItemId);

            var serverConfig = _config.GetServer();

            var ranked = top.Select(m =>
            {
                var itemId = m.MovieId != null
                    ? availableMovieItems.GetValueOrDefault(m.MovieId.Value)
                    : availableSeriesItems.GetValueOrDefault(m.SeriesId!.Value);
                var isAvailable = itemId is not null;

                string? jellyfinUrl = null;
                string? plexUrl = null;
                if (isAvailable && serverConfig is not null)
                {
                    var url = ExternalLinkHelper.BuildDeepLink(
                        serverConfig.Type, serverConfig.Url, serverConfig.MachineIdentifier, itemId);
                    if (serverConfig.Type == ServerType.Jellyfin) jellyfinUrl = url;
                    else plexUrl = url;
                }

                return new SessionMatchResultDto(
                    m.MovieId, m.SeriesId, m.Title, m.PosterPath,
                    m.AgreeCount, memberCount, isAvailable, jellyfinUrl, plexUrl);
            }).ToList();

            // ✅ Remplace les matchs précédents — un recalcul (nouveaux swipes
            // depuis la dernière révélation) doit refléter l'état courant, pas
            // s'accumuler indéfiniment.
            var existing = _db.SessionMatches.Where(m => m.SessionId == sessionId);
            _db.SessionMatches.RemoveRange(existing);

            foreach (var m in ranked)
            {
                _db.SessionMatches.Add(new SessionMatch
                {
                    Id = Guid.NewGuid(),
                    SessionId = sessionId,
                    MovieId = m.MovieId,
                    SeriesId = m.SeriesId,
                    MatchedAt = DateTime.UtcNow,
                });
            }
            await _db.SaveChangesAsync();

            return ranked;
        }

        public async Task CompleteSessionAsync(Guid sessionId)
        {
            var session = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session is null) return;
            session.Status = SessionStatus.Completed;
            // ✅ Fenêtre de grâce — le groupe doit pouvoir reconsulter les
            // résultats (rouvrir /session/{code}) pendant 2h après la fin,
            // même si la durée choisie à l'étape "démarrer" expirait plus tôt.
            session.ExpiresAt = DateTime.UtcNow.AddHours(2);
            await _db.SaveChangesAsync();
        }
    }

    // ─── DTOs ─────────────────────────────────────────────────────────────

    public record SessionMemberDto(
        Guid UserId, string DisplayName, string? AvatarUrl, bool IsGuest, bool IsHost, int SwipeCount);

    public record SessionStateDto(
        Guid Id, string Code, SessionStatus Status, Guid HostUserId,
        string[]? GenreFilter, HomeContentFilter ContentTypeFilter,
        bool HasLaunched, bool HasStarted,
        List<SessionMemberDto> Members);

    public record SessionMatchResultDto(
        Guid? MovieId, Guid? SeriesId, string Title, string? PosterPath,
        int AgreeCount, int MemberCount,
        bool IsAvailable, string? JellyfinUrl, string? PlexUrl)
    {
        public float AgreePct => MemberCount > 0 ? (float)AgreeCount / MemberCount : 0f;
    }
}
