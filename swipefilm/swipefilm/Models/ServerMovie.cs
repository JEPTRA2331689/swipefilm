namespace swipefilm.Models
{
    /// <summary>Marque un film comme présent dans la bibliothèque du serveur —
    /// fait système, une seule ligne par film (plus par serveur/utilisateur,
    /// vu qu'il n'y a qu'un serveur pour toute l'instance).</summary>
    public class ServerMovie
    {
        public Guid Id { get; set; }
        public Guid MovieId { get; set; }

        /// <summary>ID natif de l'item sur Jellyfin/Plex (ratingKey pour Plex) —
        /// permet de construire un lien direct vers la fiche du film.</summary>
        public string? ServerItemId { get; set; }

        public Movie Movie { get; set; } = null!;
    }
}
