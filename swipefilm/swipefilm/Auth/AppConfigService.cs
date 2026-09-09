// swipefilm/Auth/AppConfigService.cs
using System.Text.Json;
using swipefilm.Models;
using Hangfire;

namespace swipefilm.Auth
{
    public interface IAppConfigService
    {
        // ─── Setup ──────────────────────────────────────────────────────
        bool IsSetupComplete { get; }
        Task MarkSetupCompleteAsync();

        // ─── Adresse publique (callback webhook Radarr/Sonarr) ──────────
        string? GetPublicUrl();
        Task SetPublicUrlAsync(string? url);

        // ─── Serveur média (Jellyfin/Plex) ─────────────────────────────
        ServerSettings? GetServer();
        Task<ServerSettings> ConfigureServerAsync(AddServerDto dto);
        Task<bool> TestServerConnectionAsync(TestConnectionDto dto);
        (string url, string token) GetServerCredentials();
        Task UpdateServerSyncInfoAsync(string? machineIdentifier, DateTime lastSyncAt);

        // ─── Radarr ─────────────────────────────────────────────────────
        ArrSettings? GetRadarr();
        Task SetRadarrAsync(string url, string apiKey);
        Task RemoveRadarrAsync();
        Task SetRadarrPreferencesAsync(int? qualityProfileId, string? qualityProfileName, string? rootFolderPath);
        Task<string> GetOrCreateRadarrWebhookTokenAsync();
        Task SetRadarrWebhookConnectionIdAsync(int connectionId);

        // ─── Sonarr ─────────────────────────────────────────────────────
        ArrSettings? GetSonarr();
        Task SetSonarrAsync(string url, string apiKey);
        Task RemoveSonarrAsync();
        Task SetSonarrPreferencesAsync(int? qualityProfileId, string? qualityProfileName, string? rootFolderPath);
        Task<string> GetOrCreateSonarrWebhookTokenAsync();
        Task SetSonarrWebhookConnectionIdAsync(int connectionId);
    }

    /// <summary>
    /// Config d'instance en JSON plat (config/settings.json) — même principe
    /// qu'Overseerr/Jellyseerr : chargée une fois en mémoire au démarrage
    /// (singleton), ré-écrite en entier à chaque modification. Pas de
    /// chiffrement — le fichier vit dans un volume protégé par les
    /// permissions du système, pas par l'application.
    /// </summary>
    public class AppConfigService : IAppConfigService
    {
        private readonly string _path;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private AppConfig _config;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
        };

        public AppConfigService(IWebHostEnvironment env)
        {
            var configDir = Path.Combine(env.ContentRootPath, "config");
            Directory.CreateDirectory(configDir);
            _path = Path.Combine(configDir, "settings.json");
            _config = Load();
        }

        private AppConfig Load()
        {
            if (!File.Exists(_path)) return new AppConfig();
            try
            {
                var json = File.ReadAllText(_path);
                return JsonSerializer.Deserialize<AppConfig>(json, JsonOpts) ?? new AppConfig();
            }
            catch
            {
                return new AppConfig();
            }
        }

        private async Task SaveAsync()
        {
            await _lock.WaitAsync();
            try
            {
                var json = JsonSerializer.Serialize(_config, JsonOpts);
                await File.WriteAllTextAsync(_path, json);
            }
            finally
            {
                _lock.Release();
            }
        }

        // ─── Setup ──────────────────────────────────────────────────────

        public bool IsSetupComplete => _config.Initialized;

        public async Task MarkSetupCompleteAsync()
        {
            _config.Initialized = true;
            await SaveAsync();
        }

        public string? GetPublicUrl() => _config.PublicUrl;

        public async Task SetPublicUrlAsync(string? url)
        {
            // ✅ Un espace résiduel (copier-coller, saisie) rend l'URL
            // malformée pour Radarr/Sonarr — leur test de connectivité échoue
            // silencieusement (500) et WebhookConnectionId ne se persiste
            // jamais. TrimEnd('/') seul ne couvrait pas les espaces.
            _config.PublicUrl = string.IsNullOrWhiteSpace(url) ? null : url.Trim().TrimEnd('/');
            await SaveAsync();
        }

        // ─── Serveur média ──────────────────────────────────────────────

        public ServerSettings? GetServer() => _config.Server;

        public async Task<ServerSettings> ConfigureServerAsync(AddServerDto dto)
        {
            string token;
            string url = dto.Url?.TrimEnd('/') ?? "";

            if (dto.Type == ServerType.Jellyfin)
            {
                token = dto.ApiKey is not null
                    ? dto.ApiKey
                    : await AuthenticateJellyfinAsync(url, dto.Username!, dto.Password!);

                await ValidateJellyfinConnectionAsync(url, token);
            }
            else
            {
                if (dto.ApiKey is not null)
                {
                    token = dto.ApiKey;
                    if (string.IsNullOrEmpty(url))
                        throw new InvalidOperationException(
                            "L'URL du serveur Plex est requise avec un token direct");
                }
                else
                {
                    (token, url) = await AuthenticatePlexAsync(dto.Username!, dto.Password!);
                }
            }

            var server = _config.Server ?? new ServerSettings();
            server.Type = dto.Type;
            server.FriendlyName = dto.FriendlyName;
            server.Url = url;
            server.Token = token;
            _config.Server = server;

            await SaveAsync();

            // ✅ Sync immédiate en arrière-plan après configuration
            BackgroundJob.Enqueue<SyncBackgroundJobService>(
                "default",
                x => x.SyncSingleServerAsync());

            return server;
        }

        public async Task<bool> TestServerConnectionAsync(TestConnectionDto dto)
        {
            try
            {
                var url = dto.Url?.TrimEnd('/') ?? "";

                if (dto.Type == ServerType.Jellyfin)
                {
                    var token = dto.ApiKey ?? await AuthenticateJellyfinAsync(
                        url, dto.Username!, dto.Password!);
                    await ValidateJellyfinConnectionAsync(url, token);
                }
                else
                {
                    if (dto.ApiKey is not null)
                    {
                        if (string.IsNullOrEmpty(url))
                            return false;
                        await ValidatePlexConnectionAsync(url, dto.ApiKey);
                    }
                    else
                    {
                        await AuthenticatePlexAsync(dto.Username!, dto.Password!);
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public (string url, string token) GetServerCredentials()
        {
            var server = _config.Server
                ?? throw new KeyNotFoundException("Aucun serveur configuré");
            return (server.Url, server.Token);
        }

        public async Task UpdateServerSyncInfoAsync(string? machineIdentifier, DateTime lastSyncAt)
        {
            if (_config.Server is null) return;
            if (!string.IsNullOrEmpty(machineIdentifier))
                _config.Server.MachineIdentifier = machineIdentifier;
            _config.Server.LastSyncAt = lastSyncAt;
            await SaveAsync();
        }

        // ─── Radarr ─────────────────────────────────────────────────────

        public ArrSettings? GetRadarr() => _config.Radarr;

        public async Task SetRadarrAsync(string url, string apiKey)
        {
            var radarr = _config.Radarr ?? new ArrSettings();
            radarr.Url = url.TrimEnd('/');
            radarr.ApiKey = apiKey;
            _config.Radarr = radarr;
            await SaveAsync();
        }

        public async Task RemoveRadarrAsync()
        {
            _config.Radarr = null;
            await SaveAsync();
        }

        public async Task SetRadarrPreferencesAsync(
            int? qualityProfileId, string? qualityProfileName, string? rootFolderPath)
        {
            if (_config.Radarr is null) return;
            _config.Radarr.DefaultQualityProfileId = qualityProfileId;
            _config.Radarr.DefaultQualityProfileName = qualityProfileName;
            _config.Radarr.DefaultRootFolderPath = rootFolderPath;
            await SaveAsync();
        }

        public async Task<string> GetOrCreateRadarrWebhookTokenAsync()
        {
            if (_config.Radarr is null)
                throw new InvalidOperationException("Radarr non configuré");

            if (!string.IsNullOrEmpty(_config.Radarr.WebhookToken))
                return _config.Radarr.WebhookToken;

            var token = GenerateWebhookToken();
            _config.Radarr.WebhookToken = token;
            await SaveAsync();
            return token;
        }

        public async Task SetRadarrWebhookConnectionIdAsync(int connectionId)
        {
            if (_config.Radarr is null) return;
            _config.Radarr.WebhookConnectionId = connectionId;
            await SaveAsync();
        }

        // ─── Sonarr ─────────────────────────────────────────────────────

        public ArrSettings? GetSonarr() => _config.Sonarr;

        public async Task SetSonarrAsync(string url, string apiKey)
        {
            var sonarr = _config.Sonarr ?? new ArrSettings();
            sonarr.Url = url.TrimEnd('/');
            sonarr.ApiKey = apiKey;
            _config.Sonarr = sonarr;
            await SaveAsync();
        }

        public async Task RemoveSonarrAsync()
        {
            _config.Sonarr = null;
            await SaveAsync();
        }

        public async Task SetSonarrPreferencesAsync(
            int? qualityProfileId, string? qualityProfileName, string? rootFolderPath)
        {
            if (_config.Sonarr is null) return;
            _config.Sonarr.DefaultQualityProfileId = qualityProfileId;
            _config.Sonarr.DefaultQualityProfileName = qualityProfileName;
            _config.Sonarr.DefaultRootFolderPath = rootFolderPath;
            await SaveAsync();
        }

        public async Task<string> GetOrCreateSonarrWebhookTokenAsync()
        {
            if (_config.Sonarr is null)
                throw new InvalidOperationException("Sonarr non configuré");

            if (!string.IsNullOrEmpty(_config.Sonarr.WebhookToken))
                return _config.Sonarr.WebhookToken;

            var token = GenerateWebhookToken();
            _config.Sonarr.WebhookToken = token;
            await SaveAsync();
            return token;
        }

        public async Task SetSonarrWebhookConnectionIdAsync(int connectionId)
        {
            if (_config.Sonarr is null) return;
            _config.Sonarr.WebhookConnectionId = connectionId;
            await SaveAsync();
        }

        private static string GenerateWebhookToken() =>
            Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));

        // ─── Auth Jellyfin ────────────────────────────────────────────

        private async Task<string> AuthenticateJellyfinAsync(
            string url, string username, string password)
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add(
                "X-Emby-Authorization",
                "MediaBrowser Client=\"SwipeFilm\", Device=\"API\", DeviceId=\"swipefilm\", Version=\"1.0.0\""
            );

            var response = await http.PostAsJsonAsync(
                $"{url}/Users/AuthenticateByName",
                new { Username = username, Pw = password }
            );

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("Identifiants Jellyfin incorrects");

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            return result.GetProperty("AccessToken").GetString()
                ?? throw new InvalidOperationException("Token Jellyfin introuvable");
        }

        private async Task ValidateJellyfinConnectionAsync(string url, string token)
        {
            using var http = new HttpClient();
            var response = await http.GetAsync($"{url}/System/Configuration?api_key={token}");
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    "Connexion Jellyfin impossible — vérifie l'URL et l'API key");
        }

        // ─── Auth Plex ────────────────────────────────────────────────

        private async Task<(string token, string url)> AuthenticatePlexAsync(
            string username, string password)
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("X-Plex-Client-Identifier", "swipefilm-app");
            http.DefaultRequestHeaders.Add("X-Plex-Product", "SwipeFilm");
            http.DefaultRequestHeaders.Add("Accept", "application/json");

            var content = new FormUrlEncodedContent([
                new("user[login]", username),
                new("user[password]", password)
            ]);

            var response = await http.PostAsync(
                "https://plex.tv/users/sign_in.json", content);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("Identifiants Plex incorrects");

            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            var token = result
                .GetProperty("user")
                .GetProperty("authToken")
                .GetString()
                ?? throw new InvalidOperationException("Token Plex introuvable");

            var serverUrl = await GetPlexServerUrlAsync(token);
            return (token, serverUrl);
        }

        private async Task ValidatePlexConnectionAsync(string url, string token)
        {
            using var http = new HttpClient();
            var response = await http.GetAsync(
                $"{url}/library/sections?X-Plex-Token={token}");
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    "Connexion Plex impossible — vérifie l'URL et le token");
        }

        private async Task<string> GetPlexServerUrlAsync(string token)
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("X-Plex-Token", token);
            http.DefaultRequestHeaders.Add("Accept", "application/json");

            var response = await http.GetAsync(
                "https://plex.tv/api/v2/resources?includeHttps=1");
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();

            var server = result.EnumerateArray()
                .FirstOrDefault(r => r.GetProperty("provides").GetString() == "server");

            var connection = server
                .GetProperty("connections")
                .EnumerateArray()
                .FirstOrDefault(c => c.GetProperty("local").GetBoolean());

            return connection.GetProperty("uri").GetString()
                ?? throw new InvalidOperationException("Serveur Plex introuvable");
        }
    }

    public record AddServerDto(
        string FriendlyName,
        ServerType Type,
        string? ApiKey,
        string? Username,
        string? Password,
        string? Url
    );

    public record TestConnectionDto(
        ServerType Type,
        string? ApiKey,
        string? Username,
        string? Password,
        string? Url
    );
}
