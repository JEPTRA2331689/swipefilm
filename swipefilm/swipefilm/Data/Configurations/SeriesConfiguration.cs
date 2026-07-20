using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using swipefilm.Models;

namespace swipefilm.Data.Configurations
{
    public class SeriesConfiguration : IEntityTypeConfiguration<Series>
    {
        public void Configure(EntityTypeBuilder<Series> builder)
        {
            builder.HasKey(s => s.Id);

            builder.HasIndex(s => s.TmdbId)
                .IsUnique()
                .HasDatabaseName("idx_series_tmdbid");

            builder.HasIndex(s => s.CachedAt)
                .HasDatabaseName("idx_series_cachedat");

            builder.HasIndex(s => new { s.TmdbRating, s.TmdbPopularity })
                .HasDatabaseName("idx_series_rating_popularity");

            builder.Property(s => s.Genres).HasColumnType("text[]");
            builder.Property(s => s.Keywords).HasColumnType("text[]");
            builder.Property(s => s.CreatedBy).HasColumnType("text[]");
            builder.Property(s => s.CastTop5).HasColumnType("text[]");
        }
    }

    public class SeriesSeasonConfiguration : IEntityTypeConfiguration<SeriesSeason>
    {
        public void Configure(EntityTypeBuilder<SeriesSeason> builder)
        {
            builder.HasKey(s => s.Id);

            builder.HasIndex(s => new { s.SeriesId, s.SeasonNumber })
                .IsUnique()
                .HasDatabaseName("idx_seriesseasons_seriesid_seasonnumber");

            builder.HasOne(s => s.Series)
                .WithMany(s => s.Seasons)
                .HasForeignKey(s => s.SeriesId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class ServerSeriesConfiguration : IEntityTypeConfiguration<ServerSeries>
    {
        public void Configure(EntityTypeBuilder<ServerSeries> builder)
        {
            builder.HasKey(s => s.Id);

            // Une seule ligne par série — fait système, plus par serveur.
            builder.HasIndex(s => s.SeriesId)
                .IsUnique()
                .HasDatabaseName("idx_serverseries_series");

            builder.HasOne(s => s.Series)
                .WithMany()
                .HasForeignKey(s => s.SeriesId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class ServerSeriesSeasonConfiguration : IEntityTypeConfiguration<ServerSeriesSeason>
    {
        public void Configure(EntityTypeBuilder<ServerSeriesSeason> builder)
        {
            builder.HasKey(s => s.Id);

            // Une seule ligne par saison — fait système, plus par serveur.
            builder.HasIndex(s => s.SeriesSeasonId)
                .IsUnique()
                .HasDatabaseName("idx_serverseriesseasons_seasonid");

            builder.HasOne(s => s.SeriesSeason)
                .WithMany()
                .HasForeignKey(s => s.SeriesSeasonId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
