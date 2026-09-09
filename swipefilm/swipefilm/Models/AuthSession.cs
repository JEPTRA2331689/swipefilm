namespace swipefilm.Models
{
    /// <summary>
    /// Session de connexion — même principe qu'Overseerr : le cookie ne
    /// contient qu'un id opaque (claim "sid"), cette table est la seule
    /// source de vérité. Permet de révoquer une session à distance (logout,
    /// changement de mot de passe) sans attendre l'expiration du cookie.
    /// Nom "AuthSession" et pas "Session" — déjà pris par la session de
    /// swipe collaborative (Models/Session.cs), concept sans rapport.
    /// </summary>
    public class AuthSession
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }

        public User User { get; set; } = null!;
    }
}
