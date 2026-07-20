// swipefilm/Models/Request.cs
namespace swipefilm.Models
{
    public class MediaRequest
    {
        public Guid Id { get; set; }

        // ─── Qui demande quoi ─────────────────────────────────────────
        public Guid UserId { get; set; }

        /// <summary>Renseigné si la requête porte sur un film — exclusif avec SeriesId.</summary>
        public Guid? MovieId { get; set; }

        /// <summary>Renseigné si la requête porte sur une série — exclusif avec MovieId.</summary>
        public Guid? SeriesId { get; set; }

        // ─── Type de contenu ──────────────────────────────────────────
        public MediaRequestType Type { get; set; } = MediaRequestType.Movie;

        // ─── Statut ───────────────────────────────────────────────────
        public RequestStatus Status { get; set; } = RequestStatus.Pending;

        // ─── Métadonnées de la requête ────────────────────────────────
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }  // quand approuvé ou refusé
        public DateTime? AvailableAt { get; set; }  // quand dispo dans Jellyfin

        // ─── Traitement ───────────────────────────────────────────────
        /// <summary>Admin qui a traité la requête</summary>
        public Guid? ProcessedByUserId { get; set; }

        /// <summary>Raison du refus si Declined</summary>
        public string? DeclineReason { get; set; }

        /// <summary>ID interne Radarr/Sonarr après approbation</summary>
        public int? ExternalId { get; set; }

        /// <summary>Message optionnel de l'utilisateur</summary>
        public string? Message { get; set; }

        // ─── Overrides qualité/dossier — sinon la préférence globale de
        // l'admin (UserRadarr/UserSonarr.Default*) est utilisée à l'approbation ───
        public int? QualityProfileId { get; set; }
        public string? RootFolderPath { get; set; }

        // ─── Navigation ───────────────────────────────────────────────
        public User User { get; set; } = null!;
        public Movie? Movie { get; set; }
        public Series? Series { get; set; }
        public User? ProcessedBy { get; set; }

        /// <summary>
        /// Saisons spécifiquement demandées (Type == Tv uniquement). Vide =
        /// toute la série (comportement historique, conservé par défaut).
        /// </summary>
        public ICollection<MediaRequestSeason> Seasons { get; set; } = [];
    }

    /// <summary>Une saison précise demandée dans une requête série.</summary>
    public class MediaRequestSeason
    {
        public Guid Id { get; set; }
        public Guid MediaRequestId { get; set; }
        public int SeasonNumber { get; set; }

        public MediaRequest MediaRequest { get; set; } = null!;
    }

    public enum RequestStatus
    {
        Pending,             // en attente d'approbation
        Approved,            // approuvé, envoyé à Radarr/Sonarr
        Downloading,         // Radarr/Sonarr télécharge
        PartiallyAvailable,  // série : certaines saisons demandées sont prêtes, pas toutes
        Available,           // disponible dans Jellyfin/Plex
        Declined             // refusé par un admin
    }

    public enum MediaRequestType
    {
        Movie,  // → Radarr
        Tv      // → Sonarr
    }
}