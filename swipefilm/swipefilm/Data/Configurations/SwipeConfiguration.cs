using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using swipefilm.Models;

namespace swipefilm.Data.Configurations
{
    // Configurations/SwipeConfiguration.cs
    public class SwipeConfiguration : IEntityTypeConfiguration<Swipe>
    {
        public void Configure(EntityTypeBuilder<Swipe> builder)
        {
            builder.HasKey(s => s.Id);

            builder.HasIndex(s => s.UserId)
                .HasDatabaseName("idx_swipes_userid");

            // Index composite — requête fréquente dans GetExcludedMoviesAsync
            builder.HasIndex(s => new { s.UserId, s.Direction })
                .HasDatabaseName("idx_swipes_userid_direction");

            // Index pour les swipes récents
            builder.HasIndex(s => new { s.UserId, s.CreatedAt })
                .HasDatabaseName("idx_swipes_userid_createdat");

            builder.HasOne(s => s.Movie)
                .WithMany(m => m.Swipes)
                .HasForeignKey(s => s.MovieId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(s => s.Series)
                .WithMany(sr => sr.Swipes)
                .HasForeignKey(s => s.SeriesId)
                .OnDelete(DeleteBehavior.Cascade);

            // Exactement un des deux doit être renseigné, jamais les deux ni aucun
            builder.ToTable(t => t.HasCheckConstraint(
                "ck_swipes_movie_xor_series",
                "(\"MovieId\" IS NOT NULL) <> (\"SeriesId\" IS NOT NULL)"));
        }
    }
}
