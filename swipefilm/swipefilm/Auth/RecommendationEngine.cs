using Microsoft.EntityFrameworkCore;
using swipefilm.Data;
using swipefilm.Models;
namespace swipefilm.Auth
{
    public class RecommendationEngine
    {
        private readonly AppDbContext _db;

        public RecommendationEngine(AppDbContext db)
        {
            _db = db;
        }

        // ─── Point d'entrée ───────────────────────────────────────────

        public async Task<List<ScoredMovie>> GetRecommendationsAsync(
            Guid userId,
            Guid serverId,
            RecommendationContext context,
            int count = 20)
        {
            var profile = await GetOrCreateProfileAsync(userId);
            var excluded = await GetExcludedMoviesAsync(userId, context);
            var available = await GetAvailableMoviesAsync(serverId);

            // Pondérations dynamiques selon maturité du profil
            var weights = ComputeDynamicWeights(profile);

            var candidates = await _db.Movies
                .Where(m => !excluded.Contains(m.TmdbId)
                         && m.CachedAt != DateTime.MinValue) // Films enrichis seulement
                .ToListAsync();

            var scored = candidates
                .Select(m => new ScoredMovie(
                    m,
                    ComputeScore(m, profile, available, context, weights)))
                .ToList();

            return ApplyDiscoveryMix(scored, count, context);
        }

        // ─── Pondérations dynamiques ──────────────────────────────────

        private AlgoWeights ComputeDynamicWeights(UserProfile profile)
        {
            // Maturité = combien de signaux on a sur le profil (0 à 1)
            var maturity = Math.Min(profile.TotalSignals / 50f, 1f);

            return new AlgoWeights
            {
                // Historique : monte progressivement avec la maturité
                History = 0.10f + (0.25f * maturity),   // 10% → 35%

                // Content-based : stable mais diminue légèrement
                ContentBased = 0.30f - (0.05f * maturity),  // 30% → 25%

                // Collaboratif TMDB : reste stable
                Collaborative = 0.20f,

                // Popularité : forte au début, diminue avec la maturité
                Popularity = 0.30f - (0.20f * maturity),   // 30% → 10%

                // Disponible : reste stable
                Available = 0.05f + (0.05f * maturity)    // 5% → 10%
            };
        }

        // ─── Score principal ──────────────────────────────────────────

        private float ComputeScore(
            Movie movie,
            UserProfile profile,
            HashSet<int> available,
            RecommendationContext context,
            AlgoWeights w)
        {
            float score =
                  w.History * ComputeHistoryScore(movie, profile)
                + w.ContentBased * ComputeContentScore(movie, profile)
                + w.Collaborative * ComputePopularityScore(movie)
                + w.Popularity * NormalizeRating(movie.TmdbRating)
                + w.Available * (available.Contains(movie.TmdbId) ? 1f : 0f);

            // Pénalité si film très obscur et profil immature
            if (movie.TmdbPopularity < 5f && profile.TotalSignals < 10)
                score *= 0.5f;

            return Math.Clamp(score * GetContextMultiplier(movie, context), 0f, 1f);
        }

        // ─── Score historique (corrigé) ───────────────────────────────

        private float ComputeHistoryScore(Movie movie, UserProfile profile)
        {
            if (!profile.GenreWeights.Any()) return 0f;

            // Genres — moyenne pondérée
            float genreScore = movie.Genres.Any()
                ? movie.Genres
                    .Select(g => profile.GenreWeights.GetValueOrDefault(g, 0f))
                    .Average()
                : 0f;

            // Réalisateur — signal fort si connu
            float directorScore = movie.Directors.Any()
                ? movie.Directors
                    .Select(d => profile.DirectorWeights.GetValueOrDefault(d, 0f))
                    .Max()
                : 0f;

            // Acteurs — moyenne top 5
            float actorScore = movie.CastTop5.Any()
                ? movie.CastTop5
                    .Select(a => profile.ActorWeights.GetValueOrDefault(a, 0f))
                    .Average()
                : 0f;

            // Keywords — bonus si correspondance
            float keywordScore = movie.Keywords.Any()
                ? movie.Keywords
                    .Select(k => profile.KeywordWeights.GetValueOrDefault(k, 0f))
                    .DefaultIfEmpty(0f)
                    .Average()
                : 0f;

            return (genreScore * 0.45f)
                 + (directorScore * 0.25f)
                 + (actorScore * 0.15f)
                 + (keywordScore * 0.15f);
        }

        // ─── Score content-based (corrigé) ───────────────────────────

        private float ComputeContentScore(Movie movie, UserProfile profile)
        {
            if (!profile.GenreWeights.Any()) return 0f;

            // Similarité cosinus sur genres + keywords combinés
            var movieVector = movie.Genres
                .ToDictionary(g => g, _ => 1f);

            // Ajoute les keywords avec poids réduit
            foreach (var kw in movie.Keywords)
            {
                if (!movieVector.ContainsKey(kw))
                    movieVector[kw] = 0.5f;
            }

            var profileVector = profile.GenreWeights
                .Concat(profile.KeywordWeights
                    .ToDictionary(k => k.Key, v => v.Value * 0.5f))
                .ToDictionary(k => k.Key, v => v.Value);

            return CosineSimilarity(movieVector, profileVector);
        }

        // ─── Modificateur contextuel ──────────────────────────────────

        private float GetContextMultiplier(
            Movie movie, RecommendationContext context)
        {
            return context switch
            {
                RecommendationContext.Evening =>
                    movie.RuntimeMinutes <= 100 ? 1.3f :
                    movie.RuntimeMinutes <= 130 ? 1.0f : 0.7f,

                RecommendationContext.Discovery =>
                    movie.TmdbPopularity < 10 ? 1.5f :
                    movie.TmdbPopularity < 30 ? 1.2f : 0.8f,

                RecommendationContext.Roulette => 1f,

                _ => 1f
            };
        }

        // ─── Mix découverte (amélioré) ────────────────────────────────

        private List<ScoredMovie> ApplyDiscoveryMix(
            List<ScoredMovie> scored,
            int count,
            RecommendationContext context)
        {
            var sorted = scored.OrderByDescending(s => s.Score).ToList();

            // En mode Discovery → 40% aléatoire
            // En mode Solo/Evening → 15% aléatoire
            float discoveryRatio = context == RecommendationContext.Discovery
                ? 0.40f : 0.15f;

            int topCount = (int)(count * (1f - discoveryRatio));
            int randomCount = count - topCount;

            var top = sorted.Take(topCount).ToList();
            var rest = sorted.Skip(topCount)
                .OrderBy(_ => Random.Shared.Next())
                .Take(randomCount)
                .ToList();

            // Intercale les films de découverte entre les top
            // plutôt que les mettre tous à la fin
            var result = new List<ScoredMovie>();
            int discoveryInterval = topCount / Math.Max(randomCount, 1);
            int restIdx = 0;

            for (int i = 0; i < top.Count; i++)
            {
                result.Add(top[i]);
                if (restIdx < rest.Count
                    && i > 0
                    && i % discoveryInterval == 0)
                {
                    result.Add(rest[restIdx++]);
                }
            }

            // Ajouter les restants
            while (restIdx < rest.Count)
                result.Add(rest[restIdx++]);

            return result.Take(count).ToList();
        }

        // ─── Mise à jour profil (optimisée) ──────────────────────────

        public async Task UpdateProfileAsync(Guid userId, Guid movieId,
            SwipeDirection direction, int durationMs)
        {
            var profile = await GetOrCreateProfileAsync(userId);
            var movie = await _db.Movies.FindAsync(movieId);

            if (movie is null) return;

            // Signal selon direction + vitesse du swipe
            float signal = direction == SwipeDirection.Right
                ? durationMs < 1500 ? 0.9f : 0.7f   // Rapide = très sûr
                : durationMs < 1500 ? -0.3f : -0.1f; // Gauche rapide = dégoût fort

            // Mise à jour incrémentale — pas de rechargement complet
            UpdateWeightsIncremental(movie.Genres, signal, profile.GenreWeights);
            UpdateWeightsIncremental(movie.Directors, signal, profile.DirectorWeights);
            UpdateWeightsIncremental(movie.CastTop5, signal, profile.ActorWeights);
            UpdateWeightsIncremental(movie.Keywords, signal, profile.KeywordWeights);

            profile.TotalSignals++;
            profile.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        public async Task UpdateProfileFromHistoryAsync(Guid userId)
        {
            var profile = await GetOrCreateProfileAsync(userId);

            var history = await _db.WatchHistory
                .Include(w => w.Movie)
                .Where(w => w.UserId == userId && w.Movie != null)
                .ToListAsync();

            foreach (var w in history)
            {
                var completionPct = w.Movie!.RuntimeMinutes > 0
                    ? (float)w.WatchDurationSec
                        / (w.Movie.RuntimeMinutes!.Value * 60) * 100f
                    : 50f;

                float signal = completionPct switch
                {
                    >= 90 => 1.0f,
                    >= 70 => 0.75f,
                    >= 40 => 0.3f,
                    >= 20 => 0.0f,
                    _ => -0.3f
                };

                if (w.UserRating.HasValue)
                    signal = (signal + (w.UserRating.Value - 5f) / 5f) / 2f;

                if (w.IsFavorite) signal = MathF.Min(signal + 0.2f, 1f);
                if (w.ViewCount > 1) signal = MathF.Min(signal + 0.15f * (w.ViewCount - 1), 1f);

                UpdateWeightsIncremental(w.Movie.Genres, signal, profile.GenreWeights);
                UpdateWeightsIncremental(w.Movie.Directors, signal, profile.DirectorWeights);
                UpdateWeightsIncremental(w.Movie.CastTop5, signal, profile.ActorWeights);
                UpdateWeightsIncremental(w.Movie.Keywords, signal, profile.KeywordWeights);

                profile.TotalSignals++;
            }

            profile.GenreWeights = Normalize(profile.GenreWeights);
            profile.DirectorWeights = Normalize(profile.DirectorWeights);
            profile.ActorWeights = Normalize(profile.ActorWeights);
            profile.KeywordWeights = Normalize(profile.KeywordWeights);
            profile.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        // ─── Exclusions (corrigé) ─────────────────────────────────────

        private async Task<HashSet<int>> GetExcludedMoviesAsync(
            Guid userId, RecommendationContext context)
        {
            // Swipes gauche récents (7 jours seulement, pas 30)
            var leftSwipes = await _db.Swipes
                .Include(s => s.Movie)
                .Where(s => s.UserId == userId
                         && s.Direction == SwipeDirection.Left
                         && s.CreatedAt > DateTime.UtcNow.AddDays(-7))
                .Select(s => s.Movie.TmdbId)
                .ToListAsync();

            // Films vus à +90% → exclus sauf en mode Roulette
            var watched = await _db.WatchHistory
                .Include(w => w.Movie)
                .Where(w => w.UserId == userId)
                .ToListAsync();

            var watchedIds = context == RecommendationContext.Roulette
                ? new List<int>() // En roulette → tout est possible
                : watched
                    .Where(w => w.Movie?.RuntimeMinutes > 0 &&
                        (float)w.WatchDurationSec /
                        (w.Movie!.RuntimeMinutes!.Value * 60) >= 0.9f)
                    .Select(w => w.Movie!.TmdbId)
                    .ToList();

            // Swipes droits récents (3 jours) → dans la watchlist, pas besoin de revoir
            var rightSwipes = await _db.Swipes
                .Include(s => s.Movie)
                .Where(s => s.UserId == userId
                         && s.Direction == SwipeDirection.Right
                         && s.CreatedAt > DateTime.UtcNow.AddDays(-3))
                .Select(s => s.Movie.TmdbId)
                .ToListAsync();

            return leftSwipes
                .Concat(watchedIds)
                .Concat(rightSwipes)
                .ToHashSet();
        }

        // ─── Disponibilité (corrigé) ──────────────────────────────────

        private async Task<HashSet<int>> GetAvailableMoviesAsync(Guid serverId)
        {
            var list = await _db.Movies
                .Where(m => _db.WatchHistory
                    .Any(w => w.ServerId == serverId
                           && w.MovieId == m.Id))
                .Select(m => m.TmdbId)
                .ToListAsync();
            return list.ToHashSet();

        }

        // ─── Helpers ──────────────────────────────────────────────────

        private void UpdateWeightsIncremental(
            string[] items,
            float signal,
            Dictionary<string, float> weights)
        {
            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item)) continue;

                if (weights.TryGetValue(item, out var current))
                {
                    // EMA (Exponential Moving Average) — alpha = 0.3
                    // Donne plus de poids aux signaux récents
                    // sans effacer les anciens
                    weights[item] = (0.3f * signal) + (0.7f * current);
                }
                else
                {
                    weights[item] = signal;
                }
            }
        }

        private Dictionary<string, float> Normalize(
            Dictionary<string, float> weights)
        {
            if (!weights.Any()) return weights;
            var max = weights.Values.Max();
            if (max == 0) return weights;
            return weights.ToDictionary(k => k.Key, v => v.Value / max);
        }

        private float CosineSimilarity(
            Dictionary<string, float> a,
            Dictionary<string, float> b)
        {
            var common = a.Keys.Intersect(b.Keys).ToList();
            if (!common.Any()) return 0f;

            float dot = common.Sum(k => a[k] * b[k]);
            float normA = MathF.Sqrt(a.Values.Sum(v => v * v));
            float normB = MathF.Sqrt(b.Values.Sum(v => v * v));

            return normA == 0 || normB == 0 ? 0f : dot / (normA * normB);
        }

        private float ComputePopularityScore(Movie movie) =>
            Math.Min(movie.TmdbPopularity / 100f, 1f);

        private float NormalizeRating(float rating) =>
            Math.Clamp(rating / 10f, 0f, 1f);

        public async Task<UserProfile> GetOrCreateProfileAsync(Guid userId)
        {
            var profile = await _db.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile is not null) return profile;

            profile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                UpdatedAt = DateTime.UtcNow
            };

            _db.UserProfiles.Add(profile);
            await _db.SaveChangesAsync();

            return profile;
        }
    }

    // ─── Supporting types ─────────────────────────────────────────────

    public record ScoredMovie(Movie Movie, float Score);

    public enum RecommendationContext
    {
        Solo, Group, Evening, Discovery, Roulette
    }

    public class AlgoWeights
    {
        public float History { get; set; }
        public float ContentBased { get; set; }
        public float Collaborative { get; set; }
        public float Popularity { get; set; }
        public float Available { get; set; }
    }
}
