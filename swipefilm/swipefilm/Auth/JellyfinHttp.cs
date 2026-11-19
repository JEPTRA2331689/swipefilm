using System.Text.RegularExpressions;

namespace swipefilm.Auth
{
    // Jellyfin 10.11+ refuse le token en query string (?api_key=) : seul
    // l'en-tête Authorization: MediaBrowser Token="…" est accepté.
    public static partial class JellyfinHttp
    {
        private const string ClientInfo =
            "Client=\"SwipeFilm\", Device=\"API\", DeviceId=\"swipefilm\", Version=\"1.0.0\"";

        [GeneratedRegex(@"([?&])api_key=([^&]*)&?")]
        private static partial Regex ApiKeyParam();

        public static Task<HttpResponseMessage> GetAsync(HttpClient http, string url)
        {
            string? token = null;
            var cleanUrl = ApiKeyParam().Replace(url, m =>
            {
                token = Uri.UnescapeDataString(m.Groups[2].Value);
                return m.Groups[1].Value;
            }).TrimEnd('?', '&');

            var request = new HttpRequestMessage(HttpMethod.Get, cleanUrl);
            request.Headers.TryAddWithoutValidation("Authorization", AuthHeader(token));
            return http.SendAsync(request);
        }

        public static string AuthHeader(string? token) =>
            token is null ? $"MediaBrowser {ClientInfo}" : $"MediaBrowser {ClientInfo}, Token=\"{token}\"";
    }
}
