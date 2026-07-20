// swipefilm/Models/SectionProfile.cs
namespace swipefilm.Models
{
    /// <summary>
    /// Profil de pondération pour une section de l'app.
    /// Chaque section appelle le même moteur avec des poids différents.
    /// </summary>
    public record SectionProfile
    {
        // ─── Identité ─────────────────────────────────────────
        public string Id { get; init; } = "";
        public string Title { get; init; } = "";

        // ─── Poids des composantes ────────────────────────────

        // Historique de visionnage
        public float History { get; init; } = 0f;

        // Contenu — genres, keywords
        public float Genres { get; init; } = 0f;
        public float Keywords { get; init; } = 0f;

        // Personnes
        public float Actors { get; init; } = 0f;
        public float Directors { get; init; } = 0f;

        // Métadonnées film
        public float Popularity { get; init; } = 0f;
        public float Rating { get; init; } = 0f;
        public float Runtime { get; init; } = 0f;
        public float Year { get; init; } = 0f;

        // Disponibilité
        public float Availability { get; init; } = 0f;

        // Modificateurs spéciaux
        public float HiddenGems { get; init; } = 0f;
        public float Randomness { get; init; } = 0f;
        public float Language { get; init; } = 0f;

        // ─── Filtres & comportements ──────────────────────────

        /// <summary>Exige TmdbRating >= 6.5</summary>
        public bool QualityFilter { get; init; } = false;

        /// <summary>Cache N jours (daily=1, weekly=7)</summary>
        public int? RefreshDays { get; init; } = null;

        /// <summary>Film pivot pour "Parce que vous avez aimé"</summary>
        public Guid? BasedOnMovieId { get; init; } = null;

        /// <summary>Fenêtre temporelle pour les sorties récentes</summary>
        public int? ReleasedWithinMonths { get; init; } = null;

        /// <summary>
        /// Utilise la note Bayésienne IMDB au lieu de la note brute.
        /// WR = (v/(v+m)) × R + (m/(v+m)) × C
        /// Évite les films avec note 10 sur 2 votes.
        /// </summary>
        public bool UseBayesianRating { get; init; } = false;

        /// <summary>
        /// Nombre minimum de votes pour qu'un film soit éligible.
        /// 0 = pas de filtre.
        /// </summary>
        public int MinVoteCount { get; init; } = 0;

        /// <summary>
        /// Pré-filtre SQL sur les top acteurs du profil.
        /// Activer uniquement pour FavoriteActors.
        /// </summary>
        public bool FilterByTopActors { get; init; } = false;

        /// <summary>
        /// Pré-filtre SQL sur les top réalisateurs du profil.
        /// Activer uniquement pour FavoriteDirectors.
        /// </summary>
        public bool FilterByTopDirectors { get; init; } = false;

        /// <summary>
        /// Nombre minimum de signaux dans le profil pour un acteur/réalisateur
        /// pour qu'il soit considéré comme "top". Évite la sur-représentation
        /// d'un acteur vu dans un seul film.
        /// </summary>
        public int MinPersonSignals { get; init; } = 1;

        /// <summary>
        /// Si true, cette section ignore les exclusions habituelles
        /// (swipes droits récents) pour maximiser la découverte.
        /// </summary>
        public bool IgnoreRecentRightSwipes { get; init; } = false;

        /// <summary>
        /// Score minimum de popularité inversée pour HiddenGems.
        /// Films avec TmdbPopularity > ce seuil sont exclus.
        /// </summary>
        public float MaxPopularity { get; init; } = float.MaxValue;

        /// <summary>
        /// Pré-filtre SQL sur une liste de genres ciblés (ex: sections par genre,
        /// section saisonnière). Indépendant du profil utilisateur.
        /// </summary>
        public bool FilterByTargetGenres { get; init; } = false;

        /// <summary>Genres ciblés quand FilterByTargetGenres est activé.</summary>
        public string[]? TargetGenres { get; init; } = null;

        /// <summary>
        /// Poids d'ordre d'affichage (1-100). Les sections sont triées par poids
        /// décroissant ; les sections à poids égal sont mélangées aléatoirement
        /// entre elles.
        /// </summary>
        public int Weight { get; init; } = 50;

        // ─── Profils prédéfinis ───────────────────────────────

        /// <summary>
        /// Section principale — personnalisation maximale.
        /// History synthétise genres+acteurs+réalisateurs pondérés par complétion.
        /// Popularité faible car l'utilisateur connaît déjà les blockbusters.
        /// Pool d'exclusion global appliqué pour éviter les doublons inter-sections.
        /// </summary>
        public static SectionProfile ForYou => new()
        {
            Id = "for_you",
            Title = "Pour vous",
            History = 0.9f,
            Genres = 0.8f,
            Actors = 0.6f,
            Directors = 0.7f,
            Keywords = 0.5f,
            Rating = 0.4f,
            Popularity = 0.2f,
            Language = 0.5f,
            Year = 0.4f,
            HiddenGems = 0.2f,
            Randomness = 0.15f,
            UseBayesianRating = true,
            MinVoteCount = 10,
            Weight = 100,
        };

        /// <summary>
        /// Basé sur un film pivot (dernier film complété à 90%+ ou dernier swipe droit).
        /// Les genres et keywords du film pivot dominent.
        /// Le profil utilisateur est secondaire — l'ancrage est le film, pas l'historique.
        /// </summary>
        public static SectionProfile BecauseYouLiked(Guid movieId, string? movieTitle = null) => new()
        {
            Id = $"because_you_liked_{movieId}",
            Title = movieTitle is null
                ? "Parce que vous avez aimé"
                : $"Parce que vous avez aimé {movieTitle}",
            BasedOnMovieId = movieId,
            Genres = 1.0f,
            Directors = 0.8f,
            Keywords = 0.7f,
            Actors = 0.4f,
            Rating = 0.4f,
            History = 0.2f,
            Language = 0.3f,
            Popularity = 0.1f,
            Randomness = 0.25f,
            UseBayesianRating = true,
            MinVoteCount = 10,
            Weight = 90,
        };

        /// <summary>
        /// Pré-filtre SQL sur les top 5 acteurs du profil (score > 0.3, min 1 signal).
        /// Sans ce filtre, la section dérive vers des films génériques sans lien
        /// avec les acteurs réellement aimés.
        /// </summary>
        public static SectionProfile FavoriteActors => new()
        {
            Id = "favorite_actors",
            Title = "Vos acteurs préférés",
            Actors = 1.0f,
            History = 0.5f,
            Genres = 0.4f,
            Directors = 0.2f,
            Rating = 0.3f,
            Language = 0.3f,
            Popularity = 0.1f,
            Randomness = 0.20f,
            FilterByTopActors = true,
            MinPersonSignals = 1,
            UseBayesianRating = true,
            MinVoteCount = 10,
            Weight = 80,
        };

        /// <summary>
        /// Pré-filtre SQL sur les top 3 réalisateurs (score > 0.3, min 2 signaux).
        /// Min 2 signaux évite la sur-représentation d'un réalisateur
        /// vu dans un seul film swipé rapidement.
        /// </summary>
        public static SectionProfile FavoriteDirectors => new()
        {
            Id = "favorite_directors",
            Title = "Vos réalisateurs préférés",
            Directors = 1.0f,
            History = 0.6f,
            Genres = 0.4f,
            Actors = 0.2f,
            Rating = 0.3f,
            Language = 0.3f,
            Popularity = 0.1f,
            Randomness = 0.15f,
            FilterByTopDirectors = true,
            MinPersonSignals = 2,
            UseBayesianRating = true,
            MinVoteCount = 10,
            Weight = 80,
        };

        /// <summary>
        /// Ignore complètement le profil utilisateur — découverte objective.
        /// Note Bayésienne IMDB obligatoire + min 50 votes pour fiabilité.
        /// Popularité inversée : favorise les films peu connus mais bien notés.
        /// </summary>
        public static SectionProfile HiddenGemsList => new()
        {
            Id = "hidden_gems",
            Title = "Trésors cachés",
            HiddenGems = 1.0f,
            Rating = 1.0f,
            Genres = 0.2f,
            Popularity = 0.0f,
            Randomness = 0.30f,
            QualityFilter = true,
            UseBayesianRating = true,
            MinVoteCount = 50,
            MaxPopularity = 20f,
            Weight = 55,
        };

        /// <summary>
        /// Découverte hebdomadaire — générée une fois, cachée 7 jours.
        /// Mix profil + hasard pour éviter les répétitions.
        /// Randomness élevée car l'utilisateur voit cette section une fois par semaine.
        /// </summary>
        public static SectionProfile WeeklyDiscovery => new()
        {
            Id = "weekly_discovery",
            Title = "Découverte de la semaine",
            Genres = 0.6f,
            History = 0.5f,
            Actors = 0.4f,
            Directors = 0.4f,
            Rating = 0.6f,
            HiddenGems = 0.4f,
            Language = 0.3f,
            Randomness = 0.40f,
            RefreshDays = 7,
            UseBayesianRating = true,
            MinVoteCount = 20,
            Weight = 50,
        };

        /// <summary>
        /// Sérendipité pure. Ignore les exclusions habituelles (swipes droits récents).
        /// Qualité minimum garantie (6.5+, 20 votes min) pour éviter les films vraiment mauvais.
        /// Profil très léger — juste assez pour éviter les genres explicitement détestés.
        /// </summary>
        public static SectionProfile SurpriseMe => new()
        {
            Id = "surprise_me",
            Title = "Surprise-moi",
            Rating = 0.6f,
            Genres = 0.3f,
            History = 0.1f,
            Popularity = 0.0f,
            Randomness = 0.70f,
            QualityFilter = true,
            IgnoreRecentRightSwipes = true,
            UseBayesianRating = true,
            MinVoteCount = 20,
            Weight = 40,
        };

        /// <summary>
        /// Découverte quotidienne — générée une fois, cachée 24h.
        /// Randomness modérée : assez haute pour la surprise, assez basse pour la pertinence.
        /// </summary>
        public static SectionProfile DailyDiscovery => new()
        {
            Id = "daily_discovery",
            Title = "Découverte du jour",
            Genres = 0.7f,
            History = 0.5f,
            Actors = 0.4f,
            Directors = 0.4f,
            Rating = 0.6f,
            HiddenGems = 0.3f,
            Language = 0.4f,
            Randomness = 0.35f,
            RefreshDays = 1,
            UseBayesianRating = true,
            MinVoteCount = 15,
            Weight = 45,
        };

        /// <summary>
        /// Sorties des 24 derniers mois pertinentes pour le profil.
        /// Films récents ont naturellement moins de votes → MinVoteCount réduit à 5.
        /// Popularité légèrement favorisée : une sortie récente peu connue est un risque.
        /// QualityFilter désactivé : trop restrictif pour les films récents.
        /// </summary>
        public static SectionProfile RecentReleases => new()
        {
            Id = "recent_releases",
            Title = "Sorties récentes pour vous",
            Genres = 0.7f,
            History = 0.6f,
            Actors = 0.5f,
            Directors = 0.5f,
            Keywords = 0.4f,
            Rating = 0.5f,
            Popularity = 0.4f,
            Language = 0.6f,
            Year = 1.0f,
            Randomness = 0.20f,
            QualityFilter = false,
            ReleasedWithinMonths = 24,
            UseBayesianRating = true,
            MinVoteCount = 5,
            Weight = 60,
        };

        /// <summary>
        /// Section dynamique pour un genre dominant du profil utilisateur.
        /// Pré-filtre SQL sur le genre ciblé — les films doivent réellement
        /// appartenir à ce genre, pas juste être boostés par le score de profil.
        /// </summary>
        public static SectionProfile ForGenre(string genre) => new()
        {
            Id = $"genre_{genre}",
            Title = $"{genre} pour vous",
            Genres = 1.0f,
            History = 0.4f,
            Rating = 0.3f,
            Language = 0.3f,
            Popularity = 0.1f,
            Randomness = 0.15f,
            FilterByTargetGenres = true,
            TargetGenres = [genre],
            UseBayesianRating = true,
            MinVoteCount = 10,
            Weight = 70,
        };

        /// <summary>
        /// Section saisonnière — ignore le profil utilisateur, cible des genres
        /// liés au mois en cours. Retourne null si le mois courant n'a pas de
        /// thème associé.
        /// </summary>
        public static SectionProfile? Seasonal(int? month = null)
        {
            var (genres, title) = (month ?? DateTime.UtcNow.Month) switch
            {
                10 => (new[] { "Horreur", "Thriller" }, "Frissons d'Halloween"),
                12 => (new[] { "Familial", "Comédie", "Romance" }, "Ambiance de Noël"),
                6 or 7 or 8 => (new[] { "Action", "Aventure" }, "Blockbusters de l'été"),
                2 => (new[] { "Romance" }, "Spécial Saint-Valentin"),
                _ => (null, null)
            };

            if (genres is null) return null;

            return new SectionProfile
            {
                Id = "seasonal",
                Title = title!,
                Rating = 0.5f,
                Popularity = 0.2f,
                Randomness = 0.25f,
                QualityFilter = true,
                FilterByTargetGenres = true,
                TargetGenres = genres,
                RefreshDays = 1,
                UseBayesianRating = true,
                MinVoteCount = 15,
                Weight = 65,
            };
        }
    }
}