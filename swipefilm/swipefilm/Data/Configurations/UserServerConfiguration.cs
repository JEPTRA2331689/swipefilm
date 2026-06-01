using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using swipefilm.Models;

namespace swipefilm.Data.Configurations
{
    // Configurations/UserServerConfiguration.cs
    public class UserServerConfiguration : IEntityTypeConfiguration<UserServer>
    {
        public void Configure(EntityTypeBuilder<UserServer> builder)
        {
            builder.HasKey(s => s.Id);

            // Requête fréquente dans SyncBackgroundService
            builder.HasIndex(s => new { s.UserId, s.IsActive })
                .HasDatabaseName("idx_userservers_userid_isactive");
        }
    }
}
