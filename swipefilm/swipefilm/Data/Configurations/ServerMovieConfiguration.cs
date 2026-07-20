using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using swipefilm.Models;

namespace swipefilm.Data.Configurations
{
    public class ServerMovieConfiguration : IEntityTypeConfiguration<ServerMovie>
    {
        public void Configure(EntityTypeBuilder<ServerMovie> builder)
        {
            builder.HasKey(sm => sm.Id);

            // Une seule ligne par film — fait système, plus par serveur.
            builder.HasIndex(sm => sm.MovieId)
                .IsUnique()
                .HasDatabaseName("idx_servermovie_movie");

            builder.HasOne(sm => sm.Movie)
                .WithMany()
                .HasForeignKey(sm => sm.MovieId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
