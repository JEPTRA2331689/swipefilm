namespace swipefilm.Models
{
    /// <summary>
    /// Une ligne par saison réellement présente dans la bibliothèque Jellyfin/Plex —
    /// une série peut être partiellement disponible (ex: Sonarr n'a pas encore
    /// tout téléchargé).
    /// </summary>
    public class ServerSeriesSeason
    {
        public Guid Id { get; set; }
        public Guid SeriesSeasonId { get; set; }

        public SeriesSeason SeriesSeason { get; set; } = null!;
    }
}
