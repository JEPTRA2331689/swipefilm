// swipefilm/Auth/MediaRequestService.cs
using Microsoft.EntityFrameworkCore;
using swipefilm.Auth.swipefilm.Services;
using swipefilm.Models;

namespace swipefilm.Auth
{
    public class MediaRequestService
    {
        private readonly AppDbContext _db;
        private readonly RadarrService _radarr;
        private readonly SonarrService _sonarr;
        private readonly TmdbService _tmdb;

        public MediaRequestService(
            AppDbContext db,
            RadarrService radarr,
            SonarrService sonarr,
            TmdbService tmdb)
        {
            _db = db;
            _radarr = radarr;
            _sonarr = sonarr;
            _tmdb = tmdb;
        }

        // ─── Créer une requête ────────────────────────────────────────

        public async Task<(MediaRequest request, bool autoApproved)> CreateRequestAsync(
            Guid userId,
            Guid? movieId,
            Guid? seriesId,
            string? message,
            bool autoApprove,
            List<int>? seasonNumbers = null,
            int? qualityProfileId = null,
            string? rootFolderPath = null)
        {
            if ((movieId is null) == (seriesId is null))
                throw new InvalidOperationException(
                    "Il faut renseigner soit un film soit une série, jamais les deux ni aucun");

            var request = new MediaRequest
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MovieId = movieId,
                SeriesId = seriesId,
                Type = movieId.HasValue ? MediaRequestType.Movie : MediaRequestType.Tv,
                Status = RequestStatus.Pending,
                RequestedAt = DateTime.UtcNow,
                Message = message,
                QualityProfileId = qualityProfileId,
                RootFolderPath = rootFolderPath,
            };

            if (movieId.HasValue)
            {
                _ = await _db.Movies.FindAsync(movieId.Value)
                    ?? throw new KeyNotFoundException("Film introuvable");

                var existing = await _db.MediaRequests
                    .FirstOrDefaultAsync(r => r.UserId == userId
                                           && r.MovieId == movieId
                                           && r.Status != RequestStatus.Declined);
                if (existing is not null)
                    throw new InvalidOperationException(
                        "Une requête existe déjà pour ce film");
            }
            else
            {
                _ = await _db.Series.FindAsync(seriesId!.Value)
                    ?? throw new KeyNotFoundException("Série introuvable");

                var existing = await _db.MediaRequests
                    .FirstOrDefaultAsync(r => r.UserId == userId
                                           && r.SeriesId == seriesId
                                           && r.Status != RequestStatus.Declined);
                if (existing is not null)
                    throw new InvalidOperationException(
                        "Une requête existe déjà pour cette série");

                // ✅ Saisons précises demandées — vide/absent = toute la série
                if (seasonNumbers is { Count: > 0 })
                    request.Seasons = seasonNumbers.Distinct()
                        .Select(n => new MediaRequestSeason { Id = Guid.NewGuid(), SeasonNumber = n })
                        .ToList();
            }

            _db.MediaRequests.Add(request);

            // ✅ "Ma liste" (WatchlistController) se base uniquement sur les
            // swipes à droite solo — une requête doit donc y apparaître aussi,
            // sans dépendre d'un swipe préalable. Pas de doublon si l'un des
            // deux existe déjà (ex: déjà swipé, puis requêté ensuite).
            var alreadyOnList = await _db.Swipes.AnyAsync(s =>
                s.UserId == userId && s.SessionId == null
                && s.Direction == SwipeDirection.Right
                && (movieId.HasValue ? s.MovieId == movieId : s.SeriesId == seriesId));

            if (!alreadyOnList)
                _db.Swipes.Add(new Swipe
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    MovieId = movieId,
                    SeriesId = seriesId,
                    Direction = SwipeDirection.Right,
                    DurationMs = 0,
                    ContextMode = SwipeContext.Solo,
                    CreatedAt = DateTime.UtcNow,
                });

            await _db.SaveChangesAsync();

            // ✅ Si AutoApprove → envoie directement à Radarr/Sonarr
            if (autoApprove)
            {
                await ApproveInternalAsync(request, userId);
                return (request, true);
            }

            return (request, false);
        }

        // ─── Approuver une requête ────────────────────────────────────

        public async Task<MediaRequest> ApproveAsync(Guid requestId, Guid adminId)
        {
            var request = await _db.MediaRequests
                .Include(r => r.Movie)
                .Include(r => r.Series)
                .Include(r => r.Seasons)
                .FirstOrDefaultAsync(r => r.Id == requestId)
                ?? throw new KeyNotFoundException("Requête introuvable");

            if (request.Status != RequestStatus.Pending)
                throw new InvalidOperationException(
                    $"La requête est déjà en statut {request.Status}");

            await ApproveInternalAsync(request, adminId);
            return request;
        }

        private async Task ApproveInternalAsync(MediaRequest request, Guid processedBy)
        {
            bool success;
            string message;

            if (request.Type == MediaRequestType.Movie)
            {
                var movie = request.Movie
                    ?? await _db.Movies.FindAsync(request.MovieId!.Value)
                    ?? throw new KeyNotFoundException("Film introuvable");

                var year = movie.ReleaseDate?.Year ?? DateTime.UtcNow.Year;
                (success, message) = await _radarr.AddMovieAsync(
                    movie.TmdbId, movie.Title, year,
                    request.QualityProfileId, request.RootFolderPath);
            }
            else
            {
                var series = request.Series
                    ?? await _db.Series.FindAsync(request.SeriesId!.Value)
                    ?? throw new KeyNotFoundException("Série introuvable");

                // TMDB et TheTVDB sont deux espaces d'ID différents — Sonarr
                // attend un tvdbId, jamais le tmdbId. On le résout une seule
                // fois puis on le met en cache sur la série.
                series.TvdbId ??= await _tmdb.GetTvdbIdAsync(series.TmdbId);

                if (series.TvdbId is null)
                    throw new InvalidOperationException(
                        $"Impossible de trouver le TvdbId pour « {series.Title} » — série introuvable sur TheTVDB");

                var year = series.FirstAirDate?.Year ?? DateTime.UtcNow.Year;
                var seasonNumbers = request.Seasons.Any()
                    ? request.Seasons.Select(s => s.SeasonNumber).ToList()
                    : null;

                (success, message) = await _sonarr.AddSeriesAsync(
                    series.TvdbId.Value, series.Title, year,
                    seasonNumbers, request.QualityProfileId, request.RootFolderPath);
            }

            if (!success)
                throw new InvalidOperationException(
                    $"Erreur lors de l'envoi à {(request.Type == MediaRequestType.Movie ? "Radarr" : "Sonarr")} : {message}");

            request.Status = RequestStatus.Approved;
            request.ProcessedAt = DateTime.UtcNow;
            request.ProcessedByUserId = processedBy;

            await _db.SaveChangesAsync();
        }

        // ─── Refuser une requête ──────────────────────────────────────

        public async Task<MediaRequest> DeclineAsync(
            Guid requestId, Guid adminId, string? reason)
        {
            var request = await _db.MediaRequests
                .FirstOrDefaultAsync(r => r.Id == requestId)
                ?? throw new KeyNotFoundException("Requête introuvable");

            if (request.Status != RequestStatus.Pending)
                throw new InvalidOperationException(
                    $"La requête est déjà en statut {request.Status}");

            request.Status = RequestStatus.Declined;
            request.ProcessedAt = DateTime.UtcNow;
            request.ProcessedByUserId = adminId;
            request.DeclineReason = reason;

            await _db.SaveChangesAsync();
            return request;
        }

        // ─── Vérification quota ───────────────────────────────────────

        public async Task<(bool allowed, string? reason)> CheckQuotaAsync(
            Guid userId, MediaRequestType type)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user is null) return (false, "Utilisateur introuvable");

            // ✅ Récupère quota effectif (user override > global > null)
            int? limit;
            int days;

            if (type == MediaRequestType.Movie)
            {
                limit = user.MovieQuotaLimit
                    ?? await GetGlobalQuotaAsync("GlobalMovieQuotaLimit");
                days = user.MovieQuotaDays
                    ?? await GetGlobalQuotaAsync("GlobalMovieQuotaDays")
                    ?? 7;
            }
            else
            {
                limit = user.TvQuotaLimit
                    ?? await GetGlobalQuotaAsync("GlobalTvQuotaLimit");
                days = user.TvQuotaDays
                    ?? await GetGlobalQuotaAsync("GlobalTvQuotaDays")
                    ?? 7;
            }

            // ✅ null = illimité
            if (limit is null) return (true, null);

            var since = DateTime.UtcNow.AddDays(-days);
            var used = await _db.MediaRequests
                .CountAsync(r => r.UserId == userId
                              && r.Type == type
                              && r.Status != RequestStatus.Declined
                              && r.RequestedAt > since);

            if (used >= limit)
                return (false,
                    $"Quota atteint : {used}/{limit} requêtes sur les {days} derniers jours");

            return (true, null);
        }

        // ─── Sync statut depuis Radarr/Sonarr ────────────────────────

        /// <summary>
        /// Filet de sécurité derrière les webhooks Radarr/Sonarr — repasse sur
        /// toutes les requêtes Approved, tous utilisateurs confondus, en un
        /// seul appel bulk par service externe (pas un appel par utilisateur).
        /// </summary>
        public async Task SyncRequestStatusesAsync()
        {
            var approvedRequests = await _db.MediaRequests
                .Include(r => r.Movie)
                .Include(r => r.Series)
                .Where(r => r.Status == RequestStatus.Approved)
                .ToListAsync();

            if (!approvedRequests.Any()) return;

            var movieTmdbIds = approvedRequests
                .Where(r => r.Type == MediaRequestType.Movie && r.Movie is not null)
                .Select(r => r.Movie!.TmdbId)
                .Distinct()
                .ToList();

            if (movieTmdbIds.Any())
            {
                var statuses = await _radarr.GetBulkStatusAsync(movieTmdbIds);

                foreach (var req in approvedRequests.Where(
                    r => r.Type == MediaRequestType.Movie && r.Movie is not null))
                {
                    if (!statuses.TryGetValue(req.Movie!.TmdbId, out var status))
                        continue;

                    req.Status = status.Status switch
                    {
                        // ✅ "downloaded" chez Radarr ne veut dire que "le fichier est
                        // dans le dossier surveillé" — pas que Jellyfin/Plex l'a
                        // scanné et l'utilisateur peut le lire. Le passage à
                        // Available est confirmé uniquement par ConfirmAvailabilityAsync
                        // (déclenché par le sync Jellyfin/Plex, voir SyncSingleServerAsync).
                        "downloaded" or "downloading" or "importing" => RequestStatus.Downloading,
                        _ => req.Status
                    };
                }
            }

            var tvdbIds = approvedRequests
                .Where(r => r.Type == MediaRequestType.Tv && r.Series?.TvdbId is not null)
                .Select(r => r.Series!.TvdbId!.Value)
                .Distinct()
                .ToList();

            if (tvdbIds.Any())
            {
                var statuses = await _sonarr.GetBulkStatusAsync(tvdbIds);

                foreach (var req in approvedRequests.Where(
                    r => r.Type == MediaRequestType.Tv && r.Series?.TvdbId is not null))
                {
                    if (!statuses.TryGetValue(req.Series!.TvdbId!.Value, out var status))
                        continue;

                    req.Status = status.Status switch
                    {
                        // ✅ Même principe que pour Radarr — "downloaded"/"partial"
                        // chez Sonarr ne confirment pas que Jellyfin/Plex l'a vu.
                        // "missing" reste Approved — rien n'a encore été grab.
                        "downloaded" or "partial" or "downloading" or "importing" => RequestStatus.Downloading,
                        _ => req.Status
                    };
                }
            }

            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Confirme la vraie disponibilité (Available/PartiallyAvailable) des
        /// requêtes en Downloading, tous utilisateurs confondus — appelé juste
        /// après le sync Jellyfin/Plex (toutes les 5 min), car c'est le seul
        /// moment où on sait ce qui est réellement lisible. Un seul passage
        /// bulk, même principe que SyncRequestStatusesAsync — la disponibilité
        /// est un fait système, pas per-user, depuis qu'il n'y a qu'un serveur.
        /// </summary>
        public async Task ConfirmAvailabilityAsync()
        {
            var downloadingRequests = await _db.MediaRequests
                .Include(r => r.Seasons)
                .Where(r => r.Status == RequestStatus.Downloading)
                .ToListAsync();

            if (!downloadingRequests.Any()) return;

            foreach (var req in downloadingRequests.Where(r => r.Type == MediaRequestType.Movie))
            {
                var isAvailable = await _db.ServerMovie
                    .AnyAsync(sm => sm.MovieId == req.MovieId);

                if (!isAvailable) continue;

                req.Status = RequestStatus.Available;
                req.AvailableAt ??= DateTime.UtcNow;
            }

            foreach (var req in downloadingRequests.Where(r => r.Type == MediaRequestType.Tv))
            {
                if (!req.Seasons.Any())
                {
                    // Série entière demandée
                    var isAvailable = await _db.ServerSeries
                        .AnyAsync(ss => ss.SeriesId == req.SeriesId);

                    if (!isAvailable) continue;

                    req.Status = RequestStatus.Available;
                    req.AvailableAt ??= DateTime.UtcNow;
                    continue;
                }

                // Saisons ciblées — dispo par saison via ServerSeriesSeason
                var wantedSeasonNumbers = req.Seasons.Select(s => s.SeasonNumber).ToHashSet();

                var availableSeasonNumbers = (await _db.ServerSeriesSeasons
                    .Where(sss => sss.SeriesSeason.SeriesId == req.SeriesId)
                    .Select(sss => sss.SeriesSeason.SeasonNumber)
                    .ToListAsync())
                    .ToHashSet();

                var readyCount = wantedSeasonNumbers.Count(n => availableSeasonNumbers.Contains(n));
                if (readyCount == 0) continue;

                req.Status = readyCount == wantedSeasonNumbers.Count
                    ? RequestStatus.Available
                    : RequestStatus.PartiallyAvailable;

                if (req.Status == RequestStatus.Available)
                    req.AvailableAt ??= DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
        }

        private async Task<int?> GetGlobalQuotaAsync(string key)
        {
            var setting = await _db.AppSettings.FindAsync(key);
            return int.TryParse(setting?.Value, out var v) ? v : null;
        }
    }
}