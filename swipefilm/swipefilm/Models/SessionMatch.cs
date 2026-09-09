namespace swipefilm.Models
{
    public class SessionMatch
    {
        public Guid Id { get; set; }
        public Guid SessionId { get; set; }

        /// <summary>Renseigné si le match porte sur un film — exclusif avec SeriesId.</summary>
        public Guid? MovieId { get; set; }

        /// <summary>Renseigné si le match porte sur une série — exclusif avec MovieId.</summary>
        public Guid? SeriesId { get; set; }

        public DateTime MatchedAt { get; set; }

        // Navigation
        public Session Session { get; set; } = null!;
        public Movie? Movie { get; set; }
        public Series? Series { get; set; }
    }
}
