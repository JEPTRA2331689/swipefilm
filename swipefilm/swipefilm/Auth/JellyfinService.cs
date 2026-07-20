using System.Text.Json;
using swipefilm.Auth;

namespace swipefilm.Auth
{
    public class JellyfinService : IMediaServerService
    {
        private readonly IServerConfigService _serverService;
        private readonly HttpClient _http;

        public JellyfinService(
            IServerConfigService serverService,
            IHttpClientFactory httpClientFactory)
        {
            _serverService = serverService;
            _http = httpClientFactory.CreateClient("Jellyfin");
        }

        // ─── Bibliothèque ─────────────────────────────────────────────

        public async Task<List<MediaItem>> GetLibraryAsync(string? userId)
        {
            var (url, token) = await _serverService.GetDecryptedCredentialsAsync();

            // ✅ Si userId null → endpoint admin (pas besoin d'un user spécifique)
            // GET /Items retourne tous les films de la bibliothèque
            var baseEndpoint = !string.IsNullOrEmpty(userId)
                ? $"{url}/Users/{userId}/Items"
                : $"{url}/Items";

            var items = new List<MediaItem>();
            int startIndex = 0;
            const int pageSize = 100;
            int total;

            do
            {
                var response = await _http.GetAsync(
                    $"{baseEndpoint}" +
                    $"?IncludeItemTypes=Movie,Series" +
                    $"&Recursive=true" +
                    $"&Fields=ProviderIds,RunTimeTicks,OfficialRating" +
                    $"&StartIndex={startIndex}" +
                    $"&Limit={pageSize}" +
                    $"&api_key={token}");

                Console.WriteLine($"[Jellyfin] Library GET {response.StatusCode} — {baseEndpoint}");

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                total = json.GetProperty("TotalRecordCount").GetInt32();

                foreach (var item in json.GetProperty("Items").EnumerateArray())
                    items.Add(ParseMediaItem(item, url, token));

                startIndex += pageSize;

            } while (startIndex < total);

            return items;
        }

        // Dans Auth/JellyfinService.cs — nouvelle méthode publique
        public async Task<(string locale, string region)> GetServerLocaleAsync()
        {
            var (url, token) = await _serverService.GetDecryptedCredentialsAsync();

            try
            {
                var response = await _http.GetAsync(
                    $"{url}/System/Configuration?api_key={token}");

                if (!response.IsSuccessStatusCode) return ("en", "US");

                var json = await response.Content.ReadFromJsonAsync<JsonElement>();

                var uiCulture = json.TryGetProperty("UICulture", out var ui)
                    ? ui.GetString() ?? "en-US" : "en-US";

                var region = json.TryGetProperty("MetadataCountryCode", out var rc)
                    ? rc.GetString() ?? "US" : "US";

                var locale = uiCulture.Contains('-')
                    ? uiCulture.Split('-')[0].ToLower()
                    : uiCulture.ToLower();

                return (locale, region.ToUpper());
            }
            catch
            {
                return ("en", "US");
            }
        }

        // ─── Bibliothèque incrémentale ────────────────────────────────

        public async Task<List<MediaItem>> GetLibraryIncrementalAsync(
            DateTime? since, string? userId)
        {
            // Si pas de date → sync complète
            if (since is null)
                return await GetLibraryAsync(userId);

            var (url, token) = await _serverService
                .GetDecryptedCredentialsAsync();

            var items = new List<MediaItem>();
            int startIndex = 0;
            const int pageSize = 100;
            int total;

            // Convertit en format ISO 8601 pour Jellyfin
            var sinceStr = since.Value.ToUniversalTime()
                .ToString("yyyy-MM-ddTHH:mm:ssZ");

            // ✅ Si userId null → endpoint admin (même pattern que GetLibraryAsync)
            var baseEndpoint = !string.IsNullOrEmpty(userId)
                ? $"{url}/Users/{userId}/Items"
                : $"{url}/Items";

            do
            {
                var response = await _http.GetAsync(
                    $"{baseEndpoint}" +
                    $"?IncludeItemTypes=Movie,Series" +
                    $"&Recursive=true" +
                    $"&Fields=ProviderIds,RunTimeTicks,OfficialRating" +
                    $"&MinDateLastSaved={sinceStr}" + // ← seulement les nouveaux
                    $"&StartIndex={startIndex}" +
                    $"&Limit={pageSize}" +
                    $"&api_key={token}");

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                total = json.GetProperty("TotalRecordCount").GetInt32();

                foreach (var item in json.GetProperty("Items").EnumerateArray())
                    items.Add(ParseMediaItem(item, url, token));

                startIndex += pageSize;

            } while (startIndex < total);

            Console.WriteLine($"[Jellyfin] Sync incrémentale : {items.Count} nouveaux items depuis {since}");
            return items;
        }

        private MediaItem ParseMediaItem(JsonElement item, string url, string token)
        {
            var providerIds = item.GetProperty("ProviderIds");

            int? runtimeMin = null;
            if (item.TryGetProperty("RunTimeTicks", out var ticks))
                runtimeMin = (int)(ticks.GetInt64() / 10_000_000 / 60);

            var itemId = item.GetProperty("Id").GetString()!;
            var posterUrl = $"{url}/Items/{itemId}/Images/Primary?api_key={token}";

            return new MediaItem(
                ServerId: itemId,
                TmdbId: providerIds.TryGetProperty("Tmdb", out var tmdb)
                    ? tmdb.GetString() : null,
                ImdbId: providerIds.TryGetProperty("Imdb", out var imdb)
                    ? imdb.GetString() : null,
                Title: item.GetProperty("Name").GetString()!,
                Year: item.TryGetProperty("ProductionYear", out var year)
                    ? year.GetInt32() : null,
                Type: item.GetProperty("Type").GetString()!.ToLower(),
                PosterUrl: posterUrl,
                RuntimeMinutes: runtimeMin
            );
        }

        // ─── Historique optimisé ──────────────────────────────────────

        public async Task<List<WatchHistoryItem>> GetWatchHistoryAsync(string? userId)
            => await GetWatchHistoryIncrementalAsync(null, userId);

        public async Task<List<WatchHistoryItem>> GetWatchHistoryIncrementalAsync(
            DateTime? since, string? userId)
        {
            // ✅ Sans userId, l'historique n'a pas de sens (Played/PlayCount/
            // PlaybackPositionTicks sont par-utilisateur) — et Jellyfin renvoie
            // une 500 si on lui demande SortBy=DatePlayed sans contexte user.
            // Pas la peine de retenter, il n'y a rien à récupérer.
            if (string.IsNullOrEmpty(userId))
            {
                Console.WriteLine(
                    "[Jellyfin] Historique ignoré — aucun JellyfinUserId associé à cet utilisateur");
                return new List<WatchHistoryItem>();
            }

            var (url, token) = await _serverService
                .GetDecryptedCredentialsAsync();


            // ✅ UserData inclus directement dans la liste — plus d'appel séparé
            var items = new List<WatchHistoryItem>();
            int startIndex = 0;
            const int pageSize = 100;
            int total;
            int consecutiveUnchanged = 0;
            const int stopAfter = 20; // Arrêt anticipé si rien ne change

            // Filtre de date si sync incrémentale
            var dateFilter = since.HasValue
                ? $"&MinLastSavedDate={since.Value.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ}"
                : "";

            do
            {
                var baseEndpoint = !string.IsNullOrEmpty(userId)
                    ? $"{url}/Users/{userId}/Items"
                    : $"{url}/Items";

                var response = await _http.GetAsync(
                    $"{baseEndpoint}" +
                    $"?IncludeItemTypes=Movie,Series" +
                    $"&Recursive=true" +
                    $"&Fields=ProviderIds,UserData,RunTimeTicks" +
                    $"&SortBy=DatePlayed" +
                    $"&SortOrder=Descending" +
                    dateFilter +
                    $"&StartIndex={startIndex}" +
                    $"&Limit={pageSize}" +
                    $"&api_key={token}");

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                total = json.GetProperty("TotalRecordCount").GetInt32();

                foreach (var item in json.GetProperty("Items").EnumerateArray())
                {
                    if (!item.TryGetProperty("UserData", out var userData))
                        continue;

                    var playCount = userData.TryGetProperty("PlayCount", out var pc)
                        ? pc.GetInt32() : 0;
                    var positionTicks = userData.TryGetProperty("PlaybackPositionTicks", out var pos)
                        ? pos.GetInt64() : 0;
                    var played = userData.TryGetProperty("Played", out var p)
                        && p.GetBoolean();
                    var isFavorite = userData.TryGetProperty("IsFavorite", out var fav)
                        && fav.GetBoolean();

                    // Skip si jamais touché (sauf favoris)
                    if (playCount == 0 && positionTicks == 0 && !played && !isFavorite)
                    {
                        // ✅ Arrêt anticipé — items triés par DatePlayed desc
                        // Si on arrive à des items jamais joués, le reste est pareil
                        if (since.HasValue)
                        {
                            consecutiveUnchanged++;
                            if (consecutiveUnchanged >= stopAfter)
                            {
                                Console.WriteLine("[Jellyfin] Arrêt anticipé — pas de nouveaux items");
                                goto Done;
                            }
                        }
                        continue;
                    }

                    consecutiveUnchanged = 0;
                    var history = ParseWatchHistoryFromUserData(item, userData);
                    if (history is not null)
                        items.Add(history);
                }

                startIndex += pageSize;

            } while (startIndex < total);

        Done:
            Console.WriteLine($"[Jellyfin] {items.Count} items avec historique");
            return items;
        }

        private WatchHistoryItem? ParseWatchHistoryFromUserData(
            JsonElement item, JsonElement userData)
        {
            var providerIds = item.GetProperty("ProviderIds");

            string? tmdbId = providerIds.TryGetProperty("Tmdb", out var tmdb)
                ? tmdb.GetString() : null;

            int watchDurationSec = 0;
            int? abandonedAtSec = null;

            var positionTicks = userData.TryGetProperty("PlaybackPositionTicks", out var pos)
                ? pos.GetInt64() : 0;

            if (positionTicks > 0)
            {
                watchDurationSec = (int)(positionTicks / 10_000_000);
                abandonedAtSec = watchDurationSec;
            }

            if (item.TryGetProperty("RunTimeTicks", out var totalTicks)
                && totalTicks.GetInt64() > 0)
            {
                var totalSec = (int)(totalTicks.GetInt64() / 10_000_000);

                if (watchDurationSec >= totalSec * 0.9)
                {
                    watchDurationSec = totalSec;
                    abandonedAtSec = null;
                }
                else if (userData.TryGetProperty("Played", out var played)
                      && played.GetBoolean()
                      && positionTicks == 0)
                {
                    watchDurationSec = totalSec;
                    abandonedAtSec = null;
                }

                // Fallback PlayedPercentage
                if (watchDurationSec == 0
                    && userData.TryGetProperty("PlayedPercentage", out var pct)
                    && pct.GetDouble() > 0)
                {
                    watchDurationSec = (int)(totalSec * pct.GetDouble() / 100);
                    abandonedAtSec = watchDurationSec < totalSec * 0.9
                        ? watchDurationSec : null;
                }
            }

            DateTime lastPlayed = DateTime.UtcNow;
            if (userData.TryGetProperty("LastPlayedDate", out var lpDate)
                && lpDate.ValueKind != JsonValueKind.Null)
                lastPlayed = lpDate.GetDateTime();

            int? rating = null;
            if (userData.TryGetProperty("Rating", out var r)
                && r.ValueKind != JsonValueKind.Null)
                rating = (int)(r.GetDouble() * 10);

            int playCount = userData.TryGetProperty("PlayCount", out var pc)
                ? pc.GetInt32() : 1;

            bool isFavorite = userData.TryGetProperty("IsFavorite", out var fav)
                && fav.GetBoolean();

            // ✅ Hash pour détecter les vrais changements
            var hash = ComputeHash(watchDurationSec, playCount, isFavorite, rating);

            return new WatchHistoryItem(
                ServerId: item.GetProperty("Id").GetString()!,
                TmdbId: tmdbId,
                Title: item.GetProperty("Name").GetString()!,
                WatchDurationSec: watchDurationSec,
                AbandonedAtSec: abandonedAtSec,
                ViewCount: Math.Max(playCount, 1),
                IsFavorite: isFavorite,
                UserRating: rating,
                LastWatchedAt: lastPlayed,
                FirstWatchedAt: lastPlayed,
                ContentHash: hash
            );
        }

        // ─── Saisons ──────────────────────────────────────────────────

        public async Task<List<SeasonItem>> GetSeasonsAsync(
            string seriesServerId, string? userId)
        {
            var (url, token) = await _serverService.GetDecryptedCredentialsAsync();

            var endpoint = !string.IsNullOrEmpty(userId)
                ? $"{url}/Shows/{seriesServerId}/Seasons?userId={userId}"
                : $"{url}/Shows/{seriesServerId}/Seasons";

            var response = await _http.GetAsync(
                $"{endpoint}&Fields=ItemCounts,ChildCount&api_key={token}");

            if (!response.IsSuccessStatusCode) return [];

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            var seasons = new List<SeasonItem>();

            foreach (var season in json.GetProperty("Items").EnumerateArray())
            {
                var seasonNumber = season.TryGetProperty("IndexNumber", out var idx)
                    ? idx.GetInt32() : 0;

                var episodeCount = season.TryGetProperty("ChildCount", out var cc)
                    ? cc.GetInt32() : 0;

                var userData = season.TryGetProperty("UserData", out var ud) ? ud : default;

                var unplayed = userData.ValueKind != JsonValueKind.Undefined
                    && userData.TryGetProperty("UnplayedItemCount", out var up)
                    ? up.GetInt32() : episodeCount;

                var watchedCount = Math.Max(0, episodeCount - unplayed);

                var isFavorite = userData.ValueKind != JsonValueKind.Undefined
                    && userData.TryGetProperty("IsFavorite", out var fav)
                    && fav.GetBoolean();

                DateTime lastPlayed = DateTime.UtcNow;
                if (userData.ValueKind != JsonValueKind.Undefined
                    && userData.TryGetProperty("LastPlayedDate", out var lpDate)
                    && lpDate.ValueKind != JsonValueKind.Null)
                    lastPlayed = lpDate.GetDateTime();

                var hash = ComputeHash(watchedCount, episodeCount, isFavorite, null);

                seasons.Add(new SeasonItem(
                    ServerId: season.GetProperty("Id").GetString()!,
                    SeasonNumber: seasonNumber,
                    EpisodeCount: episodeCount,
                    WatchedEpisodeCount: watchedCount,
                    IsFavorite: isFavorite,
                    UserRating: null,
                    LastWatchedAt: lastPlayed,
                    FirstWatchedAt: lastPlayed,
                    ContentHash: hash
                ));
            }

            return seasons;
        }

        // ─── Stream URL ───────────────────────────────────────────────

        public async Task<string> GetStreamUrlAsync(string itemId)
        {
            var (url, token) = await _serverService
                .GetDecryptedCredentialsAsync();

            return $"{url}/Videos/{itemId}/stream?api_key={token}&static=true";
        }

        // ─── Helpers ──────────────────────────────────────────────────



        private static string ComputeHash(
            int watchDuration, int playCount, bool isFavorite, int? rating)
        {
            var data = $"{watchDuration}|{playCount}|{isFavorite}|{rating}";
            return Convert.ToBase64String(
                System.Security.Cryptography.MD5.HashData(
                    System.Text.Encoding.UTF8.GetBytes(data)));
        }
    }
}