namespace swipefilm.Models
{
    public class Watchlist
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }

        /// <summary>Renseigné si l'entrée porte sur un film — exclusif avec SeriesId.</summary>
        public Guid? MovieId { get; set; }

        /// <summary>Renseigné si l'entrée porte sur une série entière — exclusif avec MovieId.</summary>
        public Guid? SeriesId { get; set; }

        public int Score { get; set; }            // Score calculé par l'algo, mis à jour en background
        public DateTime AddedAt { get; set; }
        public DateTime? RemindedAt { get; set; }

        // Navigation
        public User User { get; set; } = null!;
        public Movie? Movie { get; set; }
        public Series? Series { get; set; }
    }
}
