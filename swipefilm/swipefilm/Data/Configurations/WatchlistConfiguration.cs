using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using swipefilm.Models;

namespace swipefilm.Data.Configurations
{
    public class WatchlistConfiguration : IEntityTypeConfiguration<Watchlist>
    {
        public void Configure(EntityTypeBuilder<Watchlist> builder)
        {
            builder.HasKey(w => w.Id);

            builder.HasIndex(w => w.UserId)
                .HasDatabaseName("idx_watchlists_userid");

            builder.HasOne(w => w.Movie)
                .WithMany(m => m.Watchlists)
                .HasForeignKey(w => w.MovieId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(w => w.Series)
                .WithMany(s => s.Watchlists)
                .HasForeignKey(w => w.SeriesId)
                .OnDelete(DeleteBehavior.Cascade);

            // Exactement un des deux doit être renseigné, jamais les deux ni aucun
            builder.ToTable(t => t.HasCheckConstraint(
                "ck_watchlists_movie_xor_series",
                "(\"MovieId\" IS NOT NULL) <> (\"SeriesId\" IS NOT NULL)"));
        }
    }
}
