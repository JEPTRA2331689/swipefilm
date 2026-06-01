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
        }
    }
}
