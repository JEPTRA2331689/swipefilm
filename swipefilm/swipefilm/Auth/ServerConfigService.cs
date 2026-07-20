// swipefilm/Auth/ServerConfigService.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using swipefilm.Models;
using swipefilm.Data;
using Hangfire;

namespace swipefilm.Auth
{
    public interface IServerConfigService
    {
        Task<ServerConfig> ConfigureAsync(AddServerDto dto);
        Task<ServerConfig?> GetConfigAsync();
        Task<(string url, string token)> GetDecryptedCredentialsAsync();
        Task<bool> TestConnectionAsync(TestConnectionDto dto);
    }

    // ✅ Un seul serveur pour toute l'instance — plus un serveur par
    // utilisateur. ConfigureAsync upsert l'unique ligne ServerConfig.
    public class ServerConfigService : IServerConfigService
    {
        private readonly AppDbContext _db;
        private readonly IEncryptionService _encryption;

        public ServerConfigService(AppDbContext db, IEncryptionService encryption)
        {
            _db = db;
            _encryption = encryption;
        }

        public async Task<ServerConfig> ConfigureAsync(AddServerDto dto)
        {
            string token;
            string url = dto.Url?.TrimEnd('/') ?? "";

            if (dto.Type == ServerType.Jellyfin)
            {
                // ✅ API key directe OU username/password
                token = dto.ApiKey is not null
                    ? dto.ApiKey
                    : await AuthenticateJellyfinAsync(url, dto.Username!, dto.Password!);

                // Valide que la connexion fonctionne
                await ValidateJellyfinConnectionAsync(url, token);
            }
            else
            {
                // ✅ Token Plex direct OU username/password
                if (dto.ApiKey is not null)
                {
                    token = dto.ApiKey;
                    // Pour Plex avec token direct, l'URL doit être fournie
                    if (string.IsNullOrEmpty(url))
                        throw new InvalidOperationException(
                            "L'URL du serveur Plex est requise avec un token direct");
                }
                else
                {
                    (token, url) = await AuthenticatePlexAsync(dto.Username!, dto.Password!);
                }
            }

            // ✅ Une seule ligne pour toute l'instance — met à jour si elle
            // existe déjà plutôt que d'en créer une nouvelle.
            var server = await _db.ServerConfig.FirstOrDefaultAsync();
            if (server is null)
            {
                server = new ServerConfig { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
                _db.ServerConfig.Add(server);
            }

            server.Type = dto.Type;
            server.FriendlyName = dto.FriendlyName;
            server.UrlEncrypted = _encryption.Encrypt(url);
            server.TokenEncrypted = _encryption.Encrypt(token);

            await _db.SaveChangesAsync();

            // ✅ Sync immédiate en arrière-plan après configuration
            BackgroundJob.Enqueue<SyncBackgroundJobService>(
                "default",
                x => x.SyncSingleServerAsync());

            return server;
        }

        // ✅ Test de connexion sans sauvegarder
        public async Task<bool> TestConnectionAsync(TestConnectionDto dto)
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

        public async Task<ServerConfig?> GetConfigAsync()
        {
            return await _db.ServerConfig.FirstOrDefaultAsync();
        }

        public async Task<(string url, string token)> GetDecryptedCredentialsAsync()
        {
            var server = await _db.ServerConfig.FirstOrDefaultAsync()
                ?? throw new KeyNotFoundException("Aucun serveur configuré");

            return (
                _encryption.Decrypt(server.UrlEncrypted),
                _encryption.Decrypt(server.TokenEncrypted)
            );
        }

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

    // ✅ DTO mis à jour — ApiKey optionnel, Username/Password optionnels
    public record AddServerDto(
        string FriendlyName,
        ServerType Type,
        string? ApiKey,      // ✅ API key Jellyfin ou token Plex direct
        string? Username,    // ✅ optionnel si ApiKey fourni
        string? Password,    // ✅ optionnel si ApiKey fourni
        string? Url          // ✅ requis pour Jellyfin, optionnel pour Plex (username/password)
    );

    public record TestConnectionDto(
        ServerType Type,
        string? ApiKey,
        string? Username,
        string? Password,
        string? Url
    );
}
