namespace swipefilm.Auth
{
    // Jellyfin 10.11+ refuse le token en query string (?api_key=) et l'en-tête
    // X-Emby-Authorization : seul Authorization: MediaBrowser … Token="…" passe.
    public static class JellyfinHttp
    {
        public static Task<HttpResponseMessage> GetAsync(HttpClient http, string url, string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("Authorization", AuthHeader(token));
            return http.SendAsync(request);
        }

        public static string AuthHeader(string? token, string deviceId = "swipefilm")
        {
            var header = $"MediaBrowser Client=\"SwipeFilm\", Device=\"API\", DeviceId=\"{deviceId}\", Version=\"1.0.0\"";
            return token is null ? header : $"{header}, Token=\"{token}\"";
        }
    }
}
