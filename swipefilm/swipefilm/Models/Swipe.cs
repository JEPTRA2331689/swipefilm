namespace swipefilm.Models
{
    public class Swipe
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }

        /// <summary>Renseigné si le swipe porte sur un film — exclusif avec SeriesId.</summary>
        public Guid? MovieId { get; set; }

        /// <summary>Renseigné si le swipe porte sur une série entière — exclusif avec MovieId.</summary>
        public Guid? SeriesId { get; set; }

        public SwipeDirection Direction { get; set; }
        public int DurationMs { get; set; }       // Temps passé sur la carte avant de swiper
        public SwipeContext ContextMode { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation
        public User User { get; set; } = null!;
        public Movie? Movie { get; set; }
        public Series? Series { get; set; }
    }

    public enum SwipeDirection { Left, Right }

    public enum SwipeContext
    {
        Solo,
        Group,
        Discovery,
        Evening,
        Roulette
    }
}
