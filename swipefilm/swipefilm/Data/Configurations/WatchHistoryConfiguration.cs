using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using swipefilm.Models;

namespace swipefilm.Data.Configurations
{
    public class WatchHistoryConfiguration : IEntityTypeConfiguration<WatchHistory>
    {
        public void Configure(EntityTypeBuilder<WatchHistory> builder)
        {
            builder.HasKey(w => w.Id);

            // ✅ Index les plus importants pour l'algo de reco
            builder.HasIndex(w => w.UserId)
                .HasDatabaseName("idx_watchhistory_userid");

            builder.HasIndex(w => w.ServerId)
                .HasDatabaseName("idx_watchhistory_serverid");

            builder.HasIndex(w => w.MovieId)
                .HasDatabaseName("idx_watchhistory_movieid");

            // Index composite — requête fréquente dans SyncService
            builder.HasIndex(w => new { w.UserId, w.ServerId })
                .HasDatabaseName("idx_watchhistory_userid_serverid");

            // Index pour les requêtes de l'algo (films aimés)
            builder.HasIndex(w => new { w.UserId, w.IsFavorite })
                .HasDatabaseName("idx_watchhistory_userid_isfavorite");
        }
    }
}
