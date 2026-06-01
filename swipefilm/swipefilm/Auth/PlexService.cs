using System.Xml.Linq;

namespace swipefilm.Auth
{
    public class PlexService : IMediaServerService
    {
        private readonly IUserServerService _serverService;
        private readonly HttpClient _http;

        public PlexService(
            IUserServerService serverService,
            IHttpClientFactory httpClientFactory)
        {
            _serverService = serverService;
            _http = httpClientFactory.CreateClient("Plex");
        }

        // ─── Bibliothèque ─────────────────────────────────────────────

        public async Task<List<MediaItem>> GetLibraryAsync(Guid serverId)
            => await GetLibraryIncrementalAsync(serverId, null);

        public async Task<List<MediaItem>> GetLibraryIncrementalAsync(
            Guid serverId, DateTime? since)
        {
            var (url, token) = await _serverService
                .GetDecryptedCredentialsAsync(serverId);

            var sections = await GetLibrarySectionsAsync(url, token);
            var items = new List<MediaItem>();

            foreach (var section in sections)
            {
                var sectionItems = await GetSectionItemsAsync(
                    url, token, section.Key, section.Type, since);
                items.AddRange(sectionItems);
            }

            Console.WriteLine(
                since.HasValue
                    ? $"[Plex] Sync incrémentale : {items.Count} items depuis {since}"
                    : $"[Plex] Sync complète : {items.Count} items");

            return items;
        }

        private async Task<List<(string Key, string Type)>> GetLibrarySectionsAsync(
            string url, string token)
        {
            var response = await _http.GetAsync(
                $"{url}/library/sections?X-Plex-Token={token}");

            response.EnsureSuccessStatusCode();

            var doc = XDocument.Parse(await response.Content.ReadAsStringAsync());

            return doc.Descendants("Directory")
                .Where(d => d.Attribute("type")?.Value is "movie" or "show")
                .Select(d => (
                    Key: d.Attribute("key")!.Value,
                    Type: d.Attribute("type")!.Value))
                .ToList();
        }

        private async Task<List<MediaItem>> GetSectionItemsAsync(
            string url, string token, string sectionKey, string type, DateTime? since)
        {
            var items = new List<MediaItem>();
            int start = 0;
            const int pageSize = 100;
            int total;

            // ✅ Filtre incrémental — seulement les items modifiés depuis `since`
            var sinceParam = since.HasValue
                ? $"&updatedAt>={new DateTimeOffset(since.Value).ToUnixTimeSeconds()}"
                : "";

            do
            {
                var response = await _http.GetAsync(
                    $"{url}/library/sections/{sectionKey}/all" +
                    $"?X-Plex-Token={token}" +
                    $"&X-Plex-Container-Start={start}" +
                    $"&X-Plex-Container-Size={pageSize}" +
                    sinceParam);

                response.EnsureSuccessStatusCode();

                var doc = XDocument.Parse(await response.Content.ReadAsStringAsync());

                total = int.Parse(doc.Root?.Attribute("totalSize")?.Value ?? "0");

                var tag = type == "movie" ? "Video" : "Directory";

                foreach (var item in doc.Descendants(tag))
                    items.Add(ParseMediaItem(item, url, token, type));

                start += pageSize;

            } while (start < total);

            return items;
        }

        private MediaItem ParseMediaItem(
            XElement item, string url, string token, string type)
        {
            var ratingKey = item.Attribute("ratingKey")?.Value ?? "";

            string? tmdbId = null;
            string? imdbId = null;

            foreach (var guid in item.Descendants("Guid"))
            {
                var id = guid.Attribute("id")?.Value ?? "";
                if (id.StartsWith("tmdb://"))
                    tmdbId = id.Replace("tmdb://", "");
                else if (id.StartsWith("imdb://"))
                    imdbId = id.Replace("imdb://", "");
            }

            int? runtimeMin = null;
            if (int.TryParse(item.Attribute("duration")?.Value, out var durationMs))
                runtimeMin = durationMs / 1000 / 60;

            var posterPath = item.Attribute("thumb")?.Value;
            var posterUrl = posterPath is not null
                ? $"{url}{posterPath}?X-Plex-Token={token}"
                : null;

            return new MediaItem(
                ServerId: ratingKey,
                TmdbId: tmdbId,
                ImdbId: imdbId,
                Title: item.Attribute("title")?.Value ?? "",
                Year: int.TryParse(item.Attribute("year")?.Value, out var y) ? y : null,
                Type: type,
                PosterUrl: posterUrl,
                RuntimeMinutes: runtimeMin
            );
        }

        // ─── Historique ───────────────────────────────────────────────

        public async Task<List<WatchHistoryItem>> GetWatchHistoryAsync(Guid serverId)
            => await GetWatchHistoryIncrementalAsync(serverId, null);

        public async Task<List<WatchHistoryItem>> GetWatchHistoryIncrementalAsync(
            Guid serverId, DateTime? since)
        {
            var (url, token) = await _serverService
                .GetDecryptedCredentialsAsync(serverId);

            var items = new List<WatchHistoryItem>();
            int start = 0;
            const int pageSize = 100;
            int total;
            int consecutiveOld = 0;
            const int stopAfter = 20; // ✅ Arrêt anticipé

            // ✅ Filtre incrémental — seulement depuis `since`
            var sinceParam = since.HasValue
                ? $"&viewedAt>={new DateTimeOffset(since.Value).ToUnixTimeSeconds()}"
                : "";

            do
            {
                var response = await _http.GetAsync(
                    $"{url}/status/sessions/history/all" +
                    $"?X-Plex-Token={token}" +
                    $"&sort=viewedAt:desc" +
                    $"&X-Plex-Container-Start={start}" +
                    $"&X-Plex-Container-Size={pageSize}" +
                    sinceParam);

                response.EnsureSuccessStatusCode();

                var doc = XDocument.Parse(await response.Content.ReadAsStringAsync());
                total = int.Parse(doc.Root?.Attribute("totalSize")?.Value ?? "0");

                foreach (var item in doc.Descendants("Video"))
                {
                    // ✅ Arrêt anticipé si on sort de la fenêtre temporelle
                    if (since.HasValue
                        && long.TryParse(item.Attribute("viewedAt")?.Value, out var viewedAt))
                    {
                        var viewedDate = DateTimeOffset.FromUnixTimeSeconds(viewedAt).UtcDateTime;
                        if (viewedDate < since.Value)
                        {
                            consecutiveOld++;
                            if (consecutiveOld >= stopAfter)
                            {
                                Console.WriteLine("[Plex] Arrêt anticipé — items trop anciens");
                                goto Done;
                            }
                            continue;
                        }
                        consecutiveOld = 0;
                    }

                    var history = await ParseWatchHistoryAsync(item, url, token);
                    if (history is not null)
                        items.Add(history);
                }

                start += pageSize;

            } while (start < total);

        Done:
            Console.WriteLine($"[Plex] {items.Count} items avec historique");
            return items;
        }

        private async Task<WatchHistoryItem?> ParseWatchHistoryAsync(
            XElement item, string url, string token)
        {
            var ratingKey = item.Attribute("ratingKey")?.Value;
            if (ratingKey is null) return null;

            int watchDurationSec = 0;
            int? abandonedAtSec = null;

            if (int.TryParse(item.Attribute("viewOffset")?.Value, out var offsetMs))
            {
                watchDurationSec = offsetMs / 1000;
                abandonedAtSec = watchDurationSec;
            }

            if (int.TryParse(item.Attribute("duration")?.Value, out var durationMs))
            {
                var totalSec = durationMs / 1000;

                if (watchDurationSec >= totalSec * 0.9)
                {
                    watchDurationSec = totalSec;
                    abandonedAtSec = null;
                }
            }

            DateTime lastWatched = DateTime.UtcNow;
            if (long.TryParse(item.Attribute("viewedAt")?.Value, out var viewedAt))
                lastWatched = DateTimeOffset.FromUnixTimeSeconds(viewedAt).UtcDateTime;

            // ✅ Batch les détails — récupère viewCount, rating, tmdbId en une requête
            var (viewCount, userRating, tmdbId, isFavorite) =
                await GetItemDetailsAsync(url, token, ratingKey);

            // ✅ Hash pour détecter les vrais changements
            var hash = ComputeHash(watchDurationSec, viewCount, isFavorite, userRating);

            return new WatchHistoryItem(
                ServerId: ratingKey,
                TmdbId: tmdbId,
                Title: item.Attribute("title")?.Value ?? "",
                WatchDurationSec: watchDurationSec,
                AbandonedAtSec: abandonedAtSec,
                ViewCount: viewCount,
                IsFavorite: isFavorite,
                UserRating: userRating,
                LastWatchedAt: lastWatched,
                FirstWatchedAt: lastWatched,
                ContentHash: hash
            );
        }

        private async Task<(int viewCount, int? userRating, string? tmdbId, bool isFavorite)>
            GetItemDetailsAsync(string url, string token, string ratingKey)
        {
            var response = await _http.GetAsync(
                $"{url}/library/metadata/{ratingKey}" +
                $"?X-Plex-Token={token}");

            if (!response.IsSuccessStatusCode)
                return (1, null, null, false);

            var doc = XDocument.Parse(await response.Content.ReadAsStringAsync());
            var item = doc.Descendants("Video").FirstOrDefault()
                    ?? doc.Descendants("Directory").FirstOrDefault();

            if (item is null) return (1, null, null, false);

            int viewCount = int.TryParse(
                item.Attribute("viewCount")?.Value, out var vc) ? vc : 1;

            int? userRating = null;
            if (double.TryParse(
                    item.Attribute("userRating")?.Value,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var rating))
                userRating = (int)rating;

            string? tmdbId = null;
            foreach (var guid in item.Descendants("Guid"))
            {
                var id = guid.Attribute("id")?.Value ?? "";
                if (id.StartsWith("tmdb://"))
                {
                    tmdbId = id.Replace("tmdb://", "");
                    break;
                }
            }

            // Plex n'a pas de favori natif → userRating >= 8 = favori
            bool isFavorite = userRating >= 8;

            return (viewCount, userRating, tmdbId, isFavorite);
        }

        // ─── Stream URL ───────────────────────────────────────────────

        public async Task<string> GetStreamUrlAsync(Guid serverId, string itemId)
        {
            var (url, token) = await _serverService
                .GetDecryptedCredentialsAsync(serverId);

            return $"{url}/library/parts/{itemId}/file?X-Plex-Token={token}";
        }

        // ─── Helper ───────────────────────────────────────────────────

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