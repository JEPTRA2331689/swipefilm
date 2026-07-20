namespace swipefilm.Models
{
    /// <summary>Marque une série comme présente dans la bibliothèque du serveur — miroir de ServerMovie.</summary>
    public class ServerSeries
    {
        public Guid Id { get; set; }
        public Guid SeriesId { get; set; }

        /// <summary>ID natif de l'item sur Jellyfin/Plex (ratingKey pour Plex) —
        /// permet de construire un lien direct vers la fiche de la série.</summary>
        public string? ServerItemId { get; set; }

        public Series Series { get; set; } = null!;
    }
}
