// swipefilm/Services/SeerrService.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using swipefilm.Auth;
using swipefilm.Data;
using swipefilm.Models;

namespace swipefilm.Auth
{
    public class SeerrService
    {
        private readonly IUserServerService _serverService;
        private readonly IEncryptionService _encryption;
        private readonly AppDbContext _db;
        private readonly HttpClient _http;

        public SeerrService(
            IUserServerService serverService,
            IEncryptionService encryption,
            AppDbContext db,
            IHttpClientFactory httpClientFactory)
        {
            _serverService = serverService;
            _encryption = encryption;
            _db = db;
            _http = httpClientFactory.CreateClient("Seerr");
        }

        // ─── Créer une requête ────────────────────────────────────────

        public async Task<SeerrRequestResult> RequestMediaAsync(
            Guid userId, Guid overseerrId, int tmdbId, string mediaType)
        {
            var (url, apiKey) = await GetCredentialsAsync(overseerrId);

            var body = new
            {
                mediaId = tmdbId,
                mediaType = mediaType.ToLower(), // "movie" ou "tv"
            };

            var request = new HttpRequestMessage(HttpMethod.Post,
                $"{url}/api/v1/request");
            request.Headers.Add("X-Api-Key", apiKey);
            request.Content = JsonContent.Create(body);

            var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"Seerr request failed: {response.StatusCode} — {error}");
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            var seerrRequestId = json.GetProperty("id").GetInt32().ToString();
            var status = json.TryGetProperty("status", out var s)
                ? s.GetInt32() : 1;

            // Sauvegarder en BD
            await SaveRequestAsync(userId, tmdbId, seerrRequestId, status);

            return new SeerrRequestResult(
                SeerrId: seerrRequestId,
                Status: MapStatus(status),
                Message: "Requête envoyée à Seerr ✅"
            );
        }

        // ─── Statut d'une requête ─────────────────────────────────────

        public async Task<RequestStatus> GetRequestStatusAsync(
            Guid overseerrId, string seerrRequestId)
        {
            var (url, apiKey) = await GetCredentialsAsync(overseerrId);

            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{url}/api/v1/request/{seerrRequestId}");
            request.Headers.Add("X-Api-Key", apiKey);

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return RequestStatus.Pending;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            var status = json.TryGetProperty("status", out var s)
                ? s.GetInt32() : 1;

            return MapStatus(status);
        }

        // ─── Sync statuts en background ───────────────────────────────

        public async Task SyncRequestStatusesAsync(Guid userId)
        {
            // Récupère toutes les requêtes en attente de cet user
            var pendingRequests = await _db.Requests
                .Where(r => r.UserId == userId
                         && r.Status != RequestStatus.Available
                         && r.Status != RequestStatus.Rejected
                         && r.OverseerrRequestId != null)
                .ToListAsync();

            if (!pendingRequests.Any()) return;

            // Récupère la config Seerr de l'user
            var overseerr = await _db.UserSeerr
                .FirstOrDefaultAsync(o => o.UserId == userId && o.IsActive);

            if (overseerr is null) return;

            var (url, apiKey) = (
                _encryption.Decrypt(overseerr.UrlEncrypted),
                _encryption.Decrypt(overseerr.ApiKeyEncrypted)
            );

            foreach (var req in pendingRequests)
            {
                try
                {
                    var httpReq = new HttpRequestMessage(HttpMethod.Get,
                        $"{url}/api/v1/request/{req.OverseerrRequestId}");
                    httpReq.Headers.Add("X-Api-Key", apiKey);

                    var response = await _http.SendAsync(httpReq);
                    if (!response.IsSuccessStatusCode) continue;

                    var json = await response.Content
                        .ReadFromJsonAsync<JsonElement>();

                    var status = json.TryGetProperty("status", out var s)
                        ? s.GetInt32() : 1;

                    req.Status = MapStatus(status);

                    // Si disponible → noter la date
                    if (req.Status == RequestStatus.Available)
                        req.AvailableAt = DateTime.UtcNow;
                }
                catch { /* Continue sur les autres */ }
            }

            await _db.SaveChangesAsync();
        }

        // ─── Supprimer une requête ────────────────────────────────────

        public async Task DeleteRequestAsync(
            Guid overseerrId, string seerrRequestId)
        {
            var (url, apiKey) = await GetCredentialsAsync(overseerrId);

            var request = new HttpRequestMessage(HttpMethod.Delete,
                $"{url}/api/v1/request/{seerrRequestId}");
            request.Headers.Add("X-Api-Key", apiKey);

            await _http.SendAsync(request);
        }

        // ─── Vérifier si film déjà disponible dans Seerr ─────────────

        public async Task<MediaAvailability> CheckMediaAsync(
            Guid overseerrId, int tmdbId, string mediaType)
        {
            var (url, apiKey) = await GetCredentialsAsync(overseerrId);

            var endpoint = mediaType == "movie"
                ? $"{url}/api/v1/movie/{tmdbId}"
                : $"{url}/api/v1/tv/{tmdbId}";

            var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Add("X-Api-Key", apiKey);

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return new MediaAvailability(false, false, null);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();

            // mediaInfo contient le statut dans Seerr
            if (!json.TryGetProperty("mediaInfo", out var mediaInfo))
                return new MediaAvailability(false, false, null);

            var status = mediaInfo.TryGetProperty("status", out var s)
                ? s.GetInt32() : 0;

            // Status Seerr : 1=Unknown, 2=Pending, 3=Processing,
            //                4=PartiallyAvailable, 5=Available
            return new MediaAvailability(
                IsAvailable: status == 5,
                IsRequested: status is 2 or 3 or 4,
                SeerrStatus: status
            );
        }

        // ─── Helpers ──────────────────────────────────────────────────

        private async Task<(string url, string apiKey)> GetCredentialsAsync(
            Guid overseerrId)
        {
            var overseerr = await _db.UserSeerr.FindAsync(overseerrId)
                ?? throw new KeyNotFoundException("Config Seerr introuvable");

            return (
                _encryption.Decrypt(overseerr.UrlEncrypted),
                _encryption.Decrypt(overseerr.ApiKeyEncrypted)
            );
        }

        private async Task SaveRequestAsync(
            Guid userId, int tmdbId,
            string seerrRequestId, int seerrStatus)
        {
            var movie = await _db.Movies
                .FirstOrDefaultAsync(m => m.TmdbId == tmdbId);

            if (movie is null) return;

            // Vérifie si une requête existe déjà
            var existing = await _db.Requests
                .FirstOrDefaultAsync(r =>
                    r.UserId == userId &&
                    r.MovieId == movie.Id);

            if (existing is not null)
            {
                existing.Status = MapStatus(seerrStatus);
                existing.OverseerrRequestId = seerrRequestId;
            }
            else
            {
                _db.Requests.Add(new Request
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    MovieId = movie.Id,
                    OverseerrRequestId = seerrRequestId,
                    Status = MapStatus(seerrStatus),
                    RequestedAt = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync();
        }

        private RequestStatus MapStatus(int seerrStatus) => seerrStatus switch
        {
            1 => RequestStatus.Pending,            // Unknown
            2 => RequestStatus.Pending,            // Pending approval
            3 => RequestStatus.Downloading,        // Processing
            4 => RequestStatus.Downloading,        // Partially available
            5 => RequestStatus.Available,          // Available
            _ => RequestStatus.Pending
        };
    }

    // ─── DTOs ─────────────────────────────────────────────────────────

    public record SeerrRequestResult(
        string SeerrId,
        RequestStatus Status,
        string Message
    );

    public record MediaAvailability(
        bool IsAvailable,
        bool IsRequested,
        int? SeerrStatus
    );
}