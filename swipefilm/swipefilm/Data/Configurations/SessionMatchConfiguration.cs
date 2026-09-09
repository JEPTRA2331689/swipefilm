using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using swipefilm.Models;

namespace swipefilm.Data.Configurations
{
    public class SessionMatchConfiguration : IEntityTypeConfiguration<SessionMatch>
    {
        public void Configure(EntityTypeBuilder<SessionMatch> builder)
        {
            builder.HasKey(m => m.Id);
            builder.HasIndex(m => m.SessionId).HasDatabaseName("idx_sessionmatches_sessionid");

            builder.HasOne(m => m.Session)
                .WithMany(s => s.Matches)
                .HasForeignKey(m => m.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(m => m.Movie)
                .WithMany(mv => mv.SessionMatches)
                .HasForeignKey(m => m.MovieId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(m => m.Series)
                .WithMany(sr => sr.SessionMatches)
                .HasForeignKey(m => m.SeriesId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.ToTable(t => t.HasCheckConstraint(
                "ck_sessionmatches_movie_xor_series",
                "(\"MovieId\" IS NOT NULL) <> (\"SeriesId\" IS NOT NULL)"));
        }
    }
}
