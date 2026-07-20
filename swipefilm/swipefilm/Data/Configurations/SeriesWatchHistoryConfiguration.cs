using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using swipefilm.Models;

namespace swipefilm.Data.Configurations
{
    public class SeriesWatchHistoryConfiguration : IEntityTypeConfiguration<SeriesWatchHistory>
    {
        public void Configure(EntityTypeBuilder<SeriesWatchHistory> builder)
        {
            builder.HasKey(w => w.Id);

            builder.HasIndex(w => w.UserId)
                .HasDatabaseName("idx_seriesswatchhistory_userid");

            builder.HasIndex(w => new { w.UserId, w.SeriesSeasonId })
                .IsUnique()
                .HasDatabaseName("idx_serieswatchhistory_userid_seasonid");

            builder.HasIndex(w => new { w.UserId, w.IsFavorite })
                .HasDatabaseName("idx_serieswatchhistory_userid_isfavorite");

            builder.HasOne(w => w.SeriesSeason)
                .WithMany()
                .HasForeignKey(w => w.SeriesSeasonId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
