namespace swipefilm.Models
{
    // Mots-clés TMDB (anglais, tels que stockés dans Movie.Keywords).
    public record Theme(string Id, string Title, string[] Keywords);

    public static class ThemeCatalog
    {
        public static readonly Theme[] All =
        [
            new("true_story", "Inspiré d'une histoire vraie", ["based on true story", "biography", "based on real person"]),
            new("martial_arts", "Arts martiaux", ["martial arts", "kung fu", "karate", "wuxia", "shaolin"]),
            new("parody", "Parodies", ["parody", "spoof"]),
            new("heist", "Braquages", ["heist", "bank robbery", "robbery", "heist movie"]),
            new("spy", "Espions", ["spy", "secret agent", "espionage", "spy thriller"]),
            new("buddy_cop", "Duos de flics", ["buddy cop"]),
            new("time_travel", "Voyages dans le temps", ["time travel", "time loop"]),
            new("dystopia", "Futurs sombres", ["dystopia", "post-apocalyptic future", "dystopian"]),
            new("prison", "Derrière les barreaux", ["prison", "prison escape", "prisoner"]),
            new("superhero", "Super-héros", ["superhero", "based on comic"]),
            new("gangster", "Gangsters", ["gangster", "mafia", "organized crime"]),
            new("revenge", "Vengeance", ["revenge"]),
            new("road_trip", "Road trips", ["road trip", "road movie"]),
            new("high_school", "Au lycée", ["high school", "teenager"]),
            new("zombie", "Zombies", ["zombie", "zombie apocalypse"]),
            new("stand_up", "Stand-up", ["stand-up comedy"]),
            new("space", "Dans l'espace", ["space", "outer space", "spacecraft"]),
            new("war", "Au front", ["world war ii", "world war i", "vietnam war"]),
            new("sports", "Esprit sportif", ["sports", "boxing", "football (soccer)", "basketball"]),
            new("music", "Destins musicaux", ["musician", "rock band", "biography of musician", "hip-hop"]),
        ];

        public static readonly IReadOnlyDictionary<string, Theme> ById = All.ToDictionary(t => t.Id);
    }
}
