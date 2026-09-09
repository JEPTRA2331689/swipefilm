// swipefilm/Auth/SonarrService.cs
using System.Text.Json;
using swipefilm.Models;

namespace swipefilm.Auth
{
    public class SonarrService
    {
        private readonly IAppConfigService _config;
        private readonly IHttpClientFactory _httpFactory;

        public SonarrService(
            IAppConfigService config,
            IHttpClientFactory httpFactory)
        {
            _config = config;
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

        // ─── Webhook (Connect) ──────────────────────────────────────────
        // ✅ Même principe que RadarrService.RegisterWebhookAsync — crée/met
        // à jour automatiquement la connexion "Webhook" côté Sonarr. Best-
        // effort : une erreur ici ne doit pas faire échouer la sauvegarde de
        // la config Sonarr elle-même.
        public async Task<(bool Success, string CallbackUrl, string? Error)> RegisterWebhookAsync(
            string callbackBaseUrl)
        {
            var creds = GetCredentials();
            if (creds is null) return (false, "", "Sonarr non configuré");
            var (url, apiKey) = creds.Value;

            // ✅ Même raisonnement que RadarrService : forcer http, Sonarr n'a
            // aucune raison de faire confiance au certificat TLS de ce serveur.
            if (callbackBaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                callbackBaseUrl = "http://" + callbackBaseUrl["https://".Length..];

            var token = await _config.GetOrCreateSonarrWebhookTokenAsync();
            var callbackUrl = $"{callbackBaseUrl.TrimEnd('/')}/api/webhooks/sonarr?token={token}";

            try
            {
                var existingId = _config.GetSonarr()?.WebhookConnectionId;
                var http = _httpFactory.CreateClient();

                var (ok, error) = existingId is { } id
                    ? await TryUpdateOrRecreateAsync(http, url, apiKey, id, callbackUrl)
                    : await TryCreateAsync(http, url, apiKey, callbackUrl);

                return (ok, callbackUrl, error);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Sonarr] RegisterWebhookAsync erreur: {ex.Message}");
                return (false, callbackUrl, ex.Message);
            }
        }

        private Dictionary<string, object?> BuildWebhookPayload(string callbackUrl, int? existingId) => new()
        {
            ["id"] = existingId,
            ["name"] = "SwipeFilm",
            ["implementation"] = "Webhook",
            ["implementationName"] = "Webhook",
            ["configContract"] = "WebhookSettings",
            ["onGrab"] = true,
            ["onDownload"] = true,
            ["onUpgrade"] = true,
            ["onImportComplete"] = false,
            ["onSeriesAdd"] = false,
            ["onSeriesDelete"] = false,
            ["onEpisodeFileDelete"] = false,
            ["onEpisodeFileDeleteForUpgrade"] = false,
            ["onHealthIssue"] = false,
            ["onHealthRestored"] = false,
            ["onApplicationUpdate"] = false,
            ["onManualInteractionRequired"] = false,
            ["includeHealthWarnings"] = false,
            ["tags"] = Array.Empty<int>(),
            ["fields"] = new object[]
            {
                new { name = "url", value = callbackUrl },
                new { name = "method", value = 1 }, // 1 = POST
                new { name = "username", value = "" },
                new { name = "password", value = "" },
            },
        };

        private async Task<(bool, string?)> TryCreateAsync(
            HttpClient http, string url, string apiKey, string callbackUrl)
        {
            var payload = BuildWebhookPayload(callbackUrl, null);
            payload.Remove("id");

            var response = await http.PostAsJsonAsync($"{url}/api/v3/notification?apikey={apiKey}", payload);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[Sonarr] Création webhook échouée ({(int)response.StatusCode}): {body}");
                return (false, $"Sonarr a refusé la création ({(int)response.StatusCode})");
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (json.TryGetProperty("id", out var idProp))
                await _config.SetSonarrWebhookConnectionIdAsync(idProp.GetInt32());

            return (true, null);
        }

        // ✅ Si l'id stocké a été supprimé côté Sonarr (ex: à la main dans
        // Settings → Connect), le PUT échoue avec un 404 — on retombe alors
        // sur une création plutôt que d'abandonner pour de bon.
        private async Task<(bool, string?)> TryUpdateOrRecreateAsync(
            HttpClient http, string url, string apiKey, int existingId, string callbackUrl)
        {
            var payload = BuildWebhookPayload(callbackUrl, existingId);
            var response = await http.PutAsJsonAsync(
                $"{url}/api/v3/notification/{existingId}?apikey={apiKey}", payload);

            if (response.IsSuccessStatusCode) return (true, null);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.WriteLine(
                    $"[Sonarr] Connexion webhook {existingId} introuvable (supprimée côté Sonarr ?) — recréation");
                return await TryCreateAsync(http, url, apiKey, callbackUrl);
            }

            var body = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[Sonarr] Mise à jour webhook échouée ({(int)response.StatusCode}): {body}");
            return (false, $"Sonarr a refusé la mise à jour ({(int)response.StatusCode})");
        }

        // ─── Credentials ──────────────────────────────────────────────
        // Sonarr est une config globale d'instance (config/settings.json,
        // pas une table par utilisateur) — un seul Sonarr pour tout le foyer.

        private (string url, string apiKey)? GetCredentials()
        {
            var sonarr = _config.GetSonarr();
            if (sonarr is null) return null;
            return (sonarr.Url, sonarr.ApiKey);
        }

        // ─── Profils qualité / dossiers racines ────────────────────────

        public async Task<List<(int Id, string Name)>?> GetQualityProfilesAsync()
        {
            var creds = GetCredentials();
            if (creds is null) return null;
            var (url, apiKey) = creds.Value;

            var http = _httpFactory.CreateClient();
            var response = await http.GetAsync($"{url}/api/v3/qualityprofile?apikey={apiKey}");
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            return json.EnumerateArray()
                .Select(p => (p.GetProperty("id").GetInt32(), p.GetProperty("name").GetString() ?? ""))
                .ToList();
        }

        public async Task<List<(int Id, string Path, long FreeSpace, long TotalSpace)>?> GetRootFoldersAsync()
        {
            var creds = GetCredentials();
            if (creds is null) return null;
            var (url, apiKey) = creds.Value;

            var http = _httpFactory.CreateClient();
            var response = await http.GetAsync($"{url}/api/v3/rootfolder?apikey={apiKey}");
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            return json.EnumerateArray().Select(f => (
                f.GetProperty("id").GetInt32(),
                f.GetProperty("path").GetString() ?? "",
                f.TryGetProperty("freeSpace", out var fs) ? fs.GetInt64() : 0,
                f.TryGetProperty("totalSpace", out var ts) ? ts.GetInt64() : 0
            )).ToList();
        }

        // ─── Statut d'une série ───────────────────────────────────────

        public async Task<SonarrSeriesStatus> GetSeriesStatusAsync(int tvdbId)
        {
            var creds = GetCredentials();
            if (creds is null)
                return new SonarrSeriesStatus(false, false, "notConfigured", 0, 0, null);

            var (url, apiKey) = creds.Value;

            try
            {
                var http = _httpFactory.CreateClient();

                // ✅ Filtre la collection Sonarr elle-même par tvdbId — pas de
                // lookup TVDB externe (series/lookup sert à *ajouter*, pas à
                // consulter une série déjà présente).
                var response = await http.GetAsync(
                    $"{url}/api/v3/series?tvdbId={tvdbId}&apikey={apiKey}");

                if (!response.IsSuccessStatusCode)
                    return new SonarrSeriesStatus(false, false, "notInSonarr", 0, 0, null);

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();

                if (json.ValueKind != JsonValueKind.Array)
                    return new SonarrSeriesStatus(false, false, "notInSonarr", 0, 0, null);

                var first = json.EnumerateArray().FirstOrDefault();
                if (first.ValueKind == JsonValueKind.Undefined)
                    return new SonarrSeriesStatus(false, false, "notInSonarr", 0, 0, null);

                var monitored = first.TryGetProperty("monitored", out var m) && m.GetBoolean();
                var episodeCount = first.TryGetProperty("episodeCount", out var ec) ? ec.GetInt32() : 0;
                var episodeFileCount = first.TryGetProperty("episodeFileCount", out var efc) ? efc.GetInt32() : 0;
                var sonarrId = first.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : (int?)null;

                var queue = await GetQueueBySeriesIdAsync(url, apiKey);
                var status = ResolveStatus(sonarrId, episodeFileCount, episodeCount, monitored, queue);

                return new SonarrSeriesStatus(
                    monitored, episodeFileCount > 0, status,
                    episodeCount, episodeFileCount, sonarrId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Sonarr] GetSeriesStatus erreur: {ex.Message}");
                return new SonarrSeriesStatus(false, false, "error", 0, 0, null);
            }
        }

        // ─── Queue (progression réelle des téléchargements) ───────────

        private async Task<Dictionary<int, string>> GetQueueBySeriesIdAsync(string url, string apiKey)
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
                    if (!record.TryGetProperty("seriesId", out var sidProp)) continue;
                    var seriesId = sidProp.GetInt32();

                    var state = record.TryGetProperty("trackedDownloadState", out var tds)
                        ? tds.GetString() ?? "downloading"
                        : "downloading";

                    // ✅ Plusieurs épisodes d'une même série peuvent être en
                    // queue en même temps — le premier état rencontré suffit,
                    // seule l'activité en cours compte pour ce vocabulaire.
                    if (!result.ContainsKey(seriesId))
                        result[seriesId] = state.ToLowerInvariant();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Sonarr] GetQueue erreur: {ex.Message}");
            }

            return result;
        }

        // ✅ Vocabulaire exposé : downloaded | downloading | importing |
        // partial | missing | notMonitored — même principe que Radarr, pas
        // d'état "échec" exposé (Sonarr retente en interne).
        private static string ResolveStatus(
            int? sonarrId, int episodeFileCount, int episodeCount, bool monitored,
            Dictionary<int, string> queue)
        {
            if (sonarrId is not null && queue.TryGetValue(sonarrId.Value, out var state))
            {
                return state switch
                {
                    "importpending" or "importing" => "importing",
                    "imported" => episodeFileCount >= episodeCount && episodeCount > 0 ? "downloaded" : "importing",
                    _ => "downloading", // downloading, failed, failedpending, ignored...
                };
            }

            return episodeFileCount > 0 && episodeFileCount >= episodeCount && episodeCount > 0
                ? "downloaded"
                : episodeFileCount > 0 ? "partial"
                : monitored ? "missing"
                : "notMonitored";
        }

        // ─── Ajouter une série ────────────────────────────────────────
        // seasonNumbers null/vide = toute la série (comportement historique).
        // Sinon, seules les saisons listées sont monitorées + recherchées —
        // que la série soit nouvelle pour Sonarr ou déjà présente (dans ce
        // second cas on met à jour ses saisons au lieu de re-POST /series).

        public async Task<(bool success, string message)> AddSeriesAsync(
            int tvdbId, string title, int year,
            List<int>? seasonNumbers = null,
            int? qualityProfileIdOverride = null,
            string? rootFolderPathOverride = null)
        {
            var sonarrConfig = _config.GetSonarr();
            if (sonarrConfig is null) return (false, "Sonarr non configuré");

            var url = sonarrConfig.Url;
            var apiKey = sonarrConfig.ApiKey;

            try
            {
                var http = _httpFactory.CreateClient();

                var lookupResponse = await http.GetAsync(
                    $"{url}/api/v3/series/lookup?term=tvdb:{tvdbId}&apikey={apiKey}");
                lookupResponse.EnsureSuccessStatusCode();
                var lookup = (await lookupResponse.Content.ReadFromJsonAsync<JsonElement>())
                    .EnumerateArray().FirstOrDefault();

                var existingSonarrId = lookup.ValueKind != JsonValueKind.Undefined
                    && lookup.TryGetProperty("id", out var idProp) && idProp.GetInt32() > 0
                    ? idProp.GetInt32() : (int?)null;

                // ✅ Série déjà connue de Sonarr — on ajuste ses saisons au
                // lieu de la re-créer (POST /series échouerait de toute façon).
                if (existingSonarrId is not null)
                    return await AddSeasonsToExistingSeriesAsync(
                        http, url, apiKey, existingSonarrId.Value, title, seasonNumbers);

                int qualityProfileId;
                if (qualityProfileIdOverride is not null)
                {
                    qualityProfileId = qualityProfileIdOverride.Value;
                }
                else if (sonarrConfig.DefaultQualityProfileId is not null)
                {
                    qualityProfileId = sonarrConfig.DefaultQualityProfileId.Value;
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
                else if (sonarrConfig.DefaultRootFolderPath is not null)
                {
                    rootFolder = sonarrConfig.DefaultRootFolderPath;
                }
                else
                {
                    var foldersResponse = await http.GetAsync(
                        $"{url}/api/v3/rootfolder?apikey={apiKey}");
                    foldersResponse.EnsureSuccessStatusCode();
                    var folders = await foldersResponse.Content.ReadFromJsonAsync<JsonElement>();
                    rootFolder = folders.EnumerateArray().FirstOrDefault()
                        .TryGetProperty("path", out var path)
                            ? path.GetString()
                            : "/tv"; // ✅ corrigé — /tv au lieu de /movies
                }

                // ✅ Saisons — si des saisons précises sont demandées, on ne
                // monitore que celles-là (reprend la liste de saisons de la
                // fiche Sonarr, juste avec le flag "monitored" ajusté).
                object? seasonsPayload = null;
                if (seasonNumbers is { Count: > 0 }
                    && lookup.TryGetProperty("seasons", out var lookupSeasons))
                {
                    seasonsPayload = lookupSeasons.EnumerateArray()
                        .Select(s => new
                        {
                            seasonNumber = s.GetProperty("seasonNumber").GetInt32(),
                            monitored = seasonNumbers.Contains(s.GetProperty("seasonNumber").GetInt32())
                        })
                        .ToList();
                }

                var body = new
                {
                    tvdbId,
                    title,
                    year,
                    qualityProfileId,
                    rootFolderPath = rootFolder,
                    monitored = true,
                    seasonFolder = true,
                    seasons = seasonsPayload,
                    addOptions = new
                    {
                        searchForMissingEpisodes = true,
                        monitor = seasonsPayload is null ? "all" : "none"
                    }
                };

                var addResponse = await http.PostAsJsonAsync(
                    $"{url}/api/v3/series?apikey={apiKey}", body);

                if (!addResponse.IsSuccessStatusCode)
                    return (false, $"Erreur Sonarr : {await addResponse.Content.ReadAsStringAsync()}");

                var seasonsLabel = seasonNumbers is { Count: > 0 }
                    ? $"saison(s) {string.Join(", ", seasonNumbers)}"
                    : "toutes les saisons";

                return (true, $"{title} ajouté à Sonarr ({seasonsLabel}) — recherche en cours");
            }
            catch (Exception ex)
            {
                return (false, $"Erreur : {ex.Message}");
            }
        }

        /// <summary>
        /// Série déjà présente dans Sonarr : marque les saisons demandées
        /// comme monitorées (sans en démonitorer d'autres) puis lance une
        /// recherche ciblée pour chacune.
        /// </summary>
        private async Task<(bool success, string message)> AddSeasonsToExistingSeriesAsync(
            HttpClient http, string url, string apiKey,
            int sonarrId, string title, List<int>? seasonNumbers)
        {
            var seriesResponse = await http.GetAsync(
                $"{url}/api/v3/series/{sonarrId}?apikey={apiKey}");
            if (!seriesResponse.IsSuccessStatusCode)
                return (false, "Série introuvable dans Sonarr pour mise à jour des saisons");

            var seriesJson = await seriesResponse.Content.ReadFromJsonAsync<JsonElement>();
            var seriesDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(seriesJson.GetRawText())!;

            var wantedSeasons = seasonNumbers is { Count: > 0 } ? seasonNumbers.ToHashSet() : null;
            var newlyMonitored = new List<int>();

            if (seriesJson.TryGetProperty("seasons", out var seasonsEl))
            {
                var updatedSeasons = seasonsEl.EnumerateArray().Select(s =>
                {
                    var seasonNumber = s.GetProperty("seasonNumber").GetInt32();
                    var wasMonitored = s.TryGetProperty("monitored", out var m) && m.GetBoolean();
                    // null (pas de sélection) = toute la série ; sinon on ajoute
                    // uniquement les saisons demandées sans retirer les autres.
                    var shouldMonitor = wasMonitored || wantedSeasons is null || wantedSeasons.Contains(seasonNumber);
                    if (shouldMonitor && !wasMonitored) newlyMonitored.Add(seasonNumber);
                    return new { seasonNumber, monitored = shouldMonitor };
                }).ToList();

                seriesDict["seasons"] = JsonSerializer.SerializeToElement(updatedSeasons);
                seriesDict["monitored"] = JsonSerializer.SerializeToElement(true);
            }

            var putResponse = await http.PutAsJsonAsync(
                $"{url}/api/v3/series/{sonarrId}?apikey={apiKey}", seriesDict);

            if (!putResponse.IsSuccessStatusCode)
                return (false, $"Erreur Sonarr (mise à jour saisons) : {await putResponse.Content.ReadAsStringAsync()}");

            // ✅ Recherche ciblée pour chaque saison nouvellement monitorée
            // (ou toute la série si aucune sélection précise n'a été faite)
            var targets = newlyMonitored.Any() ? newlyMonitored : wantedSeasons?.ToList() ?? [];
            foreach (var seasonNumber in targets)
            {
                await http.PostAsJsonAsync($"{url}/api/v3/command?apikey={apiKey}", new
                {
                    name = "SeasonSearch",
                    seriesId = sonarrId,
                    seasonNumber
                });
            }

            if (!targets.Any())
                await http.PostAsJsonAsync($"{url}/api/v3/command?apikey={apiKey}", new
                {
                    name = "SeriesSearch",
                    seriesId = sonarrId
                });

            var seasonsLabel = wantedSeasons is not null
                ? $"saison(s) {string.Join(", ", wantedSeasons)}"
                : "toutes les saisons";

            return (true, $"{title} — {seasonsLabel} ajoutée(s) à une série déjà présente dans Sonarr, recherche en cours");
        }

        // ─── Retirer une série ────────────────────────────────────────

        public async Task<(bool success, string message)> RemoveSeriesAsync(
            int tvdbId, bool deleteFiles = false)
        {
            var creds = GetCredentials();
            if (creds is null) return (false, "Sonarr non configuré");

            var (url, apiKey) = creds.Value;

            try
            {
                var http = _httpFactory.CreateClient();
                var status = await GetSeriesStatusAsync(tvdbId);
                if (status.SonarrId is null)
                    return (false, "Série introuvable dans Sonarr");

                var deleteUrl = $"{url}/api/v3/series/{status.SonarrId}" +
                                $"?apikey={apiKey}" +
                                $"&deleteFiles={deleteFiles.ToString().ToLower()}";

                var response = await http.DeleteAsync(deleteUrl);
                return response.IsSuccessStatusCode
                    ? (true, "Série retirée de Sonarr")
                    : (false, "Erreur lors de la suppression");
            }
            catch (Exception ex)
            {
                return (false, $"Erreur : {ex.Message}");
            }
        }

        // ─── Statuts en masse ─────────────────────────────────────────

        public async Task<Dictionary<int, SonarrSeriesStatus>> GetBulkStatusAsync(
            List<int> tvdbIds)
        {
            var result = new Dictionary<int, SonarrSeriesStatus>();
            var creds = GetCredentials();
            if (creds is null) return result;

            var (url, apiKey) = creds.Value;

            try
            {
                var http = _httpFactory.CreateClient();
                var response = await http.GetAsync($"{url}/api/v3/series?apikey={apiKey}");
                if (!response.IsSuccessStatusCode) return result;

                var allSeries = await response.Content.ReadFromJsonAsync<JsonElement>();
                var tvdbSet = tvdbIds.ToHashSet();
                var queue = await GetQueueBySeriesIdAsync(url, apiKey);

                foreach (var series in allSeries.EnumerateArray())
                {
                    if (!series.TryGetProperty("tvdbId", out var tid)) continue;
                    var id = tid.GetInt32();
                    if (!tvdbSet.Contains(id)) continue;

                    var monitored = series.TryGetProperty("monitored", out var m) && m.GetBoolean();
                    var episodeCount = series.TryGetProperty("episodeCount", out var ec) ? ec.GetInt32() : 0;
                    var episodeFileCount = series.TryGetProperty("episodeFileCount", out var efc) ? efc.GetInt32() : 0;
                    int? sonarrId = series.TryGetProperty("id", out var sid) ? sid.GetInt32() : null;

                    var status = ResolveStatus(sonarrId, episodeFileCount, episodeCount, monitored, queue);

                    result[id] = new SonarrSeriesStatus(
                        monitored, episodeFileCount > 0, status,
                        episodeCount, episodeFileCount, sonarrId);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Sonarr] GetBulkStatus erreur: {ex.Message}");
            }

            return result;
        }
    }

    public record SonarrSeriesStatus(
        bool IsMonitored,
        bool HasEpisodes,
        string Status,           // downloaded | partial | missing | notMonitored | notInSonarr | notConfigured | error
        int EpisodeCount,
        int EpisodeFileCount,
        int? SonarrId
    );
}
