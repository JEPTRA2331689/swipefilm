// swipefilm/Auth/UserServerService.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using swipefilm.Models;
using swipefilm.Data;

namespace swipefilm.Auth
{
    public interface IUserServerService
    {
        Task<UserServer> AddServerAsync(Guid userId, AddServerDto dto);
        Task<List<UserServer>> GetServersAsync(Guid userId);
        Task DeleteServerAsync(Guid userId, Guid serverId);
        Task<(string url, string token)> GetDecryptedCredentialsAsync(Guid serverId);
    }

    public class UserServerService : IUserServerService
    {
        private readonly AppDbContext _db;
        private readonly IEncryptionService _encryption;

        public UserServerService(AppDbContext db, IEncryptionService encryption)
        {
            _db = db;
            _encryption = encryption;
        }

        public async Task<UserServer> AddServerAsync(Guid userId, AddServerDto dto)
        {
            string token;
            string url = dto.Url ?? "";

            if (dto.Type == ServerType.Jellyfin)
                token = await AuthenticateJellyfinAsync(dto.Url!, dto.Username, dto.Password);
            else
                (token, url) = await AuthenticatePlexAsync(dto.Username, dto.Password);

            var server = new UserServer
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = dto.Type,
                FriendlyName = dto.FriendlyName,
                UrlEncrypted = _encryption.Encrypt(url),
                TokenEncrypted = _encryption.Encrypt(token),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.UserServers.Add(server);
            await _db.SaveChangesAsync();
            return server;
        }

        public async Task<List<UserServer>> GetServersAsync(Guid userId)
        {
            return await _db.UserServers
                .Where(s => s.UserId == userId && s.IsActive)
                .ToListAsync();
        }

        public async Task DeleteServerAsync(Guid userId, Guid serverId)
        {
            var server = await _db.UserServers
                .FirstOrDefaultAsync(s => s.Id == serverId && s.UserId == userId)
                ?? throw new KeyNotFoundException("Serveur introuvable");

            server.IsActive = false;
            await _db.SaveChangesAsync();
        }

        public async Task<(string url, string token)> GetDecryptedCredentialsAsync(Guid serverId)
        {
            var server = await _db.UserServers.FindAsync(serverId)
                ?? throw new KeyNotFoundException("Serveur introuvable");

            return (
                _encryption.Decrypt(server.UrlEncrypted),
                _encryption.Decrypt(server.TokenEncrypted)
            );
        }

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
        string Username,
        string Password,
        string? Url
    );
}