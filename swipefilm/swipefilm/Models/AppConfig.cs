namespace swipefilm.Models
{
    /// <summary>
    /// Config d'instance — un seul serveur média, un seul Radarr, un seul
    /// Sonarr pour tout le foyer. Vit dans config/settings.json, jamais en
    /// base (même principe qu'Overseerr/Jellyseerr) : c'est de la config
    /// d'admin, pas de la donnée relationnelle. En clair, pas chiffré —
    /// le fichier est protégé par les permissions du volume, pas par
    /// l'application (même choix qu'Overseerr).
    /// </summary>
    public class AppConfig
    {
        /// <summary>Même principe que le flag "public.initialized" d'Overseerr —
        /// passe à true une fois le wizard de setup terminé.</summary>
        public bool Initialized { get; set; }
        public ServerSettings? Server { get; set; }
        public ArrSettings? Radarr { get; set; }
        public ArrSettings? Sonarr { get; set; }

        /// <summary>Adresse à laquelle Radarr/Sonarr peuvent joindre SwipeFilm
        /// pour le webhook — pas forcément la même que celle utilisée par le
        /// navigateur de l'admin (Request.Host, le fallback par défaut) si
        /// Radarr/Sonarr tourne sur une autre machine que celle d'où l'admin
        /// configure l'instance. Ex: "http://10.0.0.45:8000".</summary>
        public string? PublicUrl { get; set; }
    }

    public class ServerSettings
    {
        public ServerType Type { get; set; }
        public string FriendlyName { get; set; } = "";
        public string Url { get; set; } = "";
        public string Token { get; set; } = "";

        /// <summary>Plex uniquement — nécessaire pour construire un lien direct
        /// vers une fiche (app.plex.tv/desktop#!/server/{MachineIdentifier}/...).</summary>
        public string? MachineIdentifier { get; set; }
        public DateTime? LastSyncAt { get; set; }
    }

    // ✅ Même forme pour Radarr et Sonarr — pas besoin de deux classes identiques
    public class ArrSettings
    {
        public string Url { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public int? DefaultQualityProfileId { get; set; }
        public string? DefaultQualityProfileName { get; set; }
        public string? DefaultRootFolderPath { get; set; }

        /// <summary>Généré une seule fois à la première configuration — plus
        /// besoin de le créer/coller à la main dans .env (même principe
        /// qu'Overseerr, qui gère ses propres secrets de connexion Radarr/
        /// Sonarr). Vérifié par RadarrWebhookController/SonarrWebhookController
        /// sur chaque requête entrante.</summary>
        public string? WebhookToken { get; set; }

        /// <summary>Id de la connexion "Webhook" créée côté Radarr/Sonarr
        /// (api/v3/notification) — permet de la mettre à jour plutôt que d'en
        /// recréer une en double à chaque reconfiguration.</summary>
        public int? WebhookConnectionId { get; set; }
    }

    public enum ServerType { Plex, Jellyfin }
}
