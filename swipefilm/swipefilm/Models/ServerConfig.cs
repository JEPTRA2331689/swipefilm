namespace swipefilm.Models
{
    /// <summary>
    /// Le serveur Jellyfin/Plex de l'instance — une seule ligne, partagée par
    /// tous les utilisateurs (pas de multi-serveur : un foyer, un serveur).
    /// Remplace l'ancien UserServer (qui dupliquait ces mêmes URL/token par
    /// utilisateur alors qu'ils étaient toujours identiques).
    /// </summary>
    public class ServerConfig
    {
        public Guid Id { get; set; }
        public ServerType Type { get; set; }
        public string FriendlyName { get; set; } = null!;
        public string UrlEncrypted { get; set; } = null!;
        public string TokenEncrypted { get; set; } = null!;
        public DateTime? LastSyncAt { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>Plex uniquement — nécessaire pour construire un lien direct
        /// vers une fiche (app.plex.tv/desktop#!/server/{MachineIdentifier}/...).
        /// Récupéré et mis en cache au premier sync du serveur.</summary>
        public string? MachineIdentifier { get; set; }
    }

    public enum ServerType { Plex, Jellyfin }
}
