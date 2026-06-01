using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using swipefilm.Models;

namespace swipefilm.Data.Configurations
{
    public class MovieConfiguration : IEntityTypeConfiguration<Movie>
    {
        public void Configure(EntityTypeBuilder<Movie> builder)
        {
            builder.HasKey(m => m.Id);

            // Unique sur TmdbId — requête très fréquente
            builder.HasIndex(m => m.TmdbId)
                .IsUnique()
                .HasDatabaseName("idx_movies_tmdbid");

            // Index pour EnrichAllMoviesAsync
            builder.HasIndex(m => m.CachedAt)
                .HasDatabaseName("idx_movies_cachedat");

            // Index pour filtrer par type
            builder.HasIndex(m => m.ContentType)
                .HasDatabaseName("idx_movies_contenttype");

            // Index pour l'algo — films bien notés
            builder.HasIndex(m => new { m.TmdbRating, m.TmdbPopularity })
                .HasDatabaseName("idx_movies_rating_popularity");

            // Tableaux PostgreSQL natifs
            builder.Property(m => m.Genres)
                .HasColumnType("text[]");
            builder.Property(m => m.Keywords)
                .HasColumnType("text[]");
            builder.Property(m => m.Directors)
                .HasColumnType("text[]");
            builder.Property(m => m.CastTop5)
                .HasColumnType("text[]");
        }
    }

}
