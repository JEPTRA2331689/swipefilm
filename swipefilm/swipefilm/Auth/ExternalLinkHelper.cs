// swipefilm/Auth/ExternalLinkHelper.cs
using swipefilm.Models;

namespace swipefilm.Auth
{
    /// <summary>
    /// Construit un lien direct vers la fiche d'un film/série sur Jellyfin ou
    /// Plex — nécessite l'ID natif de l'item (ServerMovie/ServerSeries.ServerItemId)
    /// et, pour Plex, le MachineIdentifier du serveur (UserServer.MachineIdentifier).
    /// </summary>
    public static class ExternalLinkHelper
    {
        public static string? BuildDeepLink(
            ServerType serverType, string serverUrl, string? machineIdentifier, string? serverItemId)
        {
            if (string.IsNullOrEmpty(serverItemId)) return null;

            var baseUrl = serverUrl.TrimEnd('/');

            return serverType switch
            {
                ServerType.Jellyfin => $"{baseUrl}/web/index.html#!/details?id={serverItemId}",
                ServerType.Plex when !string.IsNullOrEmpty(machineIdentifier) =>
                    $"{baseUrl}/web/index.html#!/server/{machineIdentifier}/details?key=%2Flibrary%2Fmetadata%2F{serverItemId}",
                _ => null,
            };
        }
    }
}
