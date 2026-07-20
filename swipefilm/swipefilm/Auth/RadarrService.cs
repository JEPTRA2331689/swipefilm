// swipefilm/Auth/RadarrService.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using swipefilm.Models;

namespace swipefilm.Auth
{
    public class RadarrService
    {
        private readonly AppDbContext _db;
        private readonly IEncryptionService _encryption;
        private readonly IHttpClientFactory _httpFactory;

        public RadarrService(
            AppDbContext db,
            IEncryptionService encryption,
            IHttpClientFactory httpFactory)
        {
            _db = db;
            _encryption = encryption;
            _httpFactory = httpFactory;
        }

        // ─── Connexion ────────────────────────────────────────────────

        public async Task<bool> TestConnectionAsync(string url, string apiKey)
        {
            try
            {
                var http = _httpFactory.CreateClient();
                var response = await http.GetAsync(
                    $"{url.TrimEnd('/')}/api/v3/system/status?apikey={apiKey}");
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        // ─── Credentials ──────────────────────────────────────────────
        // Radarr est une config globale d'instance (un seul admin la configure
        // via [RequirePermission(Permission.Admin)]) — pas une config par
        // utilisateur, donc pas de filtre UserId ici.

        private async Task<UserRadarr?> GetActiveConfigAsync()
        {
            return await _db.UserRadarr
                .Include(r => r.User)
                .Where(r => r.IsActive && (r.User.Permissions & (long)Permission.Admin) != 0)
                .FirstOrDefaultAsync();
        }

        private async Task<(string url, string apiKey)?> GetCredentialsAsync()
        {
            var radarr = await GetActiveConfigAsync();
            if (radarr is null) return null;

            return (
                _encryption.Decrypt(radarr.UrlEncrypted),
                _encryption.Decrypt(radarr.ApiKeyEncrypted)
            );
        }

        // ─── Statut d'un film ─────────────────────────────────────────

        public async Task<RadarrMovieStatus> GetMovieStatusAsync(int tmdbId)
        {
            var creds = await GetCredentialsAsync();
            if (creds is null)
                return new RadarrMovieStatus(false, false, "notConfigured", null, null);

            var (url, apiKey) = creds.Value;

            try
            {
                var http = _httpFactory.CreateClient();

                // ✅ Filtre la collection Radarr elle-même par tmdbId — pas de
                // lookup TMDB externe (movie/lookup/tmdb sert à *ajouter*, pas
                // à consulter un film déjà présent).
                var response = await http.GetAsync(
                    $"{url}/api/v3/movie?tmdbId={tmdbId}&apikey={apiKey}");

                if (!response.IsSuccessStatusCode)
                    return new RadarrMovieStatus(false, false, "notInRadarr", null, null);

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();

                if (json.ValueKind != JsonValueKind.Array)
                    return new RadarrMovieStatus(false, false, "notInRadarr", null, null);

                var first = json.EnumerateArray().FirstOrDefault();
                if (first.ValueKind == JsonValueKind.Undefined)
                    return new RadarrMovieStatus(false, false, "notInRadarr", null, null);

                var monitored = first.TryGetProperty("monitored", out var m) && m.GetBoolean();
                var hasFile = first.TryGetProperty("hasFile", out var hf) && hf.GetBoolean();
                long? size = first.TryGetProperty("sizeOnDisk", out var s) ? s.GetInt64() : null;
                var radarrId = first.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : (int?)null;

                var queue = await GetQueueByMovieIdAsync(url, apiKey);
                var status = ResolveStatus(radarrId, hasFile, monitored, queue);

                return new RadarrMovieStatus(monitored, hasFile, status, size, radarrId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Radarr] GetMovieStatus erreur: {ex.Message}");
                return new RadarrMovieStatus(false, false, "error", null, null);
            }
        }

        // ─── Queue (progression réelle des téléchargements) ───────────

        private async Task<Dictionary<int, string>> GetQueueByMovieIdAsync(string url, string apiKey)
        {
            var result = new Dictionary<int, string>();

            try
            {
                var http = _httpFactory.CreateClient();
                var response = await http.GetAsync(
                    $"{url}/api/v3/queue?pageSize=1000&apikey={apiKey}");
                if (!response.IsSuccessStatusCode) return result;

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                if (!json.TryGetProperty("records", out var records)) return result;

                foreach (var record in records.EnumerateArray())
                {
                    if (!record.TryGetProperty("movieId", out var midProp)) continue;
                    var movieId = midProp.GetInt32();

                    var state = record.TryGetProperty("trackedDownloadState", out var tds)
                        ? tds.GetString() ?? "downloading"
                        : "downloading";

                    result[movieId] = state.ToLowerInvariant();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Radarr] GetQueue erreur: {ex.Message}");
            }

            return result;
        }

        // ✅ Vocabulaire exposé : downloaded | downloading | importing |
        // missing | notMonitored — la queue prime sur hasFile/monitored car
        // elle seule reflète une activité de téléchargement réelle.
        // ⚠️ Pas d'état "échec" exposé : un échec Radarr (release ratée,
        // indexeur en panne...) reste affiché comme "downloading" côté
        // utilisateur — Radarr retente en interne, l'utilisateur n'a pas
        // besoin de voir cette mécanique.
        private static string ResolveStatus(
            int? radarrId, bool hasFile, bool monitored, Dictionary<int, string> queue)
        {
            if (radarrId is not null && queue.TryGetValue(radarrId.Value, out var state))
            {
                return state switch
                {
                    "importpending" or "importing" => "importing",
                    "imported" => hasFile ? "downloaded" : "importing",
                    _ => "downloading", // downloading, failed, failedpending, ignored...
                };
            }

            return hasFile ? "downloaded"
                 : monitored ? "missing"
                 : "notMonitored";
        }

        // ─── Ajouter un film ──────────────────────────────────────────

        public async Task<(bool success, string message)> AddMovieAsync(
            int tmdbId, string title, int year,
            int? qualityProfileIdOverride = null,
            string? rootFolderPathOverride = null)
        {
            var radarrConfig = await GetActiveConfigAsync();
            if (radarrConfig is null) return (false, "Radarr non configuré");

            var url = _encryption.Decrypt(radarrConfig.UrlEncrypted);
            var apiKey = _encryption.Decrypt(radarrConfig.ApiKeyEncrypted);

            try
            {
                var http = _httpFactory.CreateClient();

                // ✅ Priorité : choix explicite de la demande > préférence
                // admin sauvegardée > premier profil disponible
                int qualityProfileId;
                if (qualityProfileIdOverride is not null)
                {
                    qualityProfileId = qualityProfileIdOverride.Value;
                }
                else if (radarrConfig?.DefaultQualityProfileId is not null)
                {
                    qualityProfileId = radarrConfig.DefaultQualityProfileId.Value;
                }
                else
                {
                    var profilesResponse = await http.GetAsync(
                        $"{url}/api/v3/qualityprofile?apikey={apiKey}");
                    profilesResponse.EnsureSuccessStatusCode();
                    var profiles = await profilesResponse.Content.ReadFromJsonAsync<JsonElement>();
                    qualityProfileId = profiles.EnumerateArray().FirstOrDefault()
                        .TryGetProperty("id", out var pid) ? pid.GetInt32() : 1;
                }

                string? rootFolder;
                if (rootFolderPathOverride is not null)
                {
                    rootFolder = rootFolderPathOverride;
                }
                else if (radarrConfig?.DefaultRootFolderPath is not null)
                {
                    rootFolder = radarrConfig.DefaultRootFolderPath;
                }
                else
                {
                    var foldersResponse = await http.GetAsync(
                        $"{url}/api/v3/rootfolder?apikey={apiKey}");
                    foldersResponse.EnsureSuccessStatusCode();
                    var folders = await foldersResponse.Content.ReadFromJsonAsync<JsonElement>();
                    rootFolder = folders.EnumerateArray().FirstOrDefault()
                        .TryGetProperty("path", out var path) ? path.GetString() : "/movies";
                }

                var body = new
                {
                    tmdbId,
                    title,
                    year,
                    qualityProfileId,
                    rootFolderPath = rootFolder,
                    monitored = true,
                    addOptions = new { searchForMovie = true }
                };

                var addResponse = await http.PostAsJsonAsync(
                    $"{url}/api/v3/movie?apikey={apiKey}", body);

                return addResponse.IsSuccessStatusCode
                    ? (true, $"{title} ajouté à Radarr — recherche en cours")
                    : (false, $"Erreur Radarr : {await addResponse.Content.ReadAsStringAsync()}");
            }
            catch (Exception ex)
            {
                return (false, $"Erreur : {ex.Message}");
            }
        }

        // ─── Retirer un film ──────────────────────────────────────────

        public async Task<(bool success, string message)> RemoveMovieAsync(
            int tmdbId, bool deleteFiles = false)
        {
            var creds = await GetCredentialsAsync();
            if (creds is null) return (false, "Radarr non configuré");

            var (url, apiKey) = creds.Value;

            try
            {
                var http = _httpFactory.CreateClient();

                // ✅ Cherche l'ID interne Radarr du film
                var status = await GetMovieStatusAsync(tmdbId);
                if (status.RadarrId is null)
                    return (false, "Film introuvable dans Radarr");

                var deleteUrl = $"{url}/api/v3/movie/{status.RadarrId}" +
                                $"?apikey={apiKey}" +
                                $"&deleteFiles={deleteFiles.ToString().ToLower()}" +
                                $"&addImportExclusion=false";

                var response = await http.DeleteAsync(deleteUrl);
                return response.IsSuccessStatusCode
                    ? (true, "Film retiré de Radarr")
                    : (false, "Erreur lors de la suppression");
            }
            catch (Exception ex)
            {
                return (false, $"Erreur : {ex.Message}");
            }
        }

        // ─── Statuts en masse ─────────────────────────────────────────

        public async Task<Dictionary<int, RadarrMovieStatus>> GetBulkStatusAsync(
            List<int> tmdbIds)
        {
            var result = new Dictionary<int, RadarrMovieStatus>();
            var creds = await GetCredentialsAsync();
            if (creds is null) return result;

            var (url, apiKey) = creds.Value;

            try
            {
                // ✅ Récupère tous les films Radarr en une seule requête
                var http = _httpFactory.CreateClient();
                var response = await http.GetAsync($"{url}/api/v3/movie?apikey={apiKey}");
                if (!response.IsSuccessStatusCode) return result;

                var allMovies = await response.Content.ReadFromJsonAsync<JsonElement>();
                var tmdbSet = tmdbIds.ToHashSet();
                var queue = await GetQueueByMovieIdAsync(url, apiKey);

                foreach (var movie in allMovies.EnumerateArray())
                {
                    if (!movie.TryGetProperty("tmdbId", out var tid)) continue;
                    var id = tid.GetInt32();
                    if (!tmdbSet.Contains(id)) continue;

                    var monitored = movie.TryGetProperty("monitored", out var m) && m.GetBoolean();
                    var hasFile = movie.TryGetProperty("hasFile", out var hf) && hf.GetBoolean();
                    long? size = movie.TryGetProperty("sizeOnDisk", out var s) ? s.GetInt64() : null;
                    int? radarrId = movie.TryGetProperty("id", out var rid) ? rid.GetInt32() : null;

                    var status = ResolveStatus(radarrId, hasFile, monitored, queue);

                    result[id] = new RadarrMovieStatus(monitored, hasFile, status, size, radarrId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Radarr] GetBulkStatus erreur: {ex.Message}");
            }

            return result;
        }
    }

    public record RadarrMovieStatus(
        bool IsMonitored,
        bool HasFile,
        string Status,       // downloaded | missing | notMonitored | notInRadarr | notConfigured | error
        long? SizeOnDisk,
        int? RadarrId
    );
}
