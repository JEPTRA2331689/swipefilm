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

            // Index composite — évite les doublons
            builder.HasIndex(sm => new { sm.ServerId, sm.MovieId })
                .IsUnique()
                .HasDatabaseName("idx_servermovie_server_movie");

            builder.HasOne(sm => sm.Server)
                .WithMany()
                .HasForeignKey(sm => sm.ServerId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(sm => sm.Movie)
                .WithMany()
                .HasForeignKey(sm => sm.MovieId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
