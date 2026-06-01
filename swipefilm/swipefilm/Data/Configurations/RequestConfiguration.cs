using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using swipefilm.Models;

namespace swipefilm.Data.Configurations
{
    public class RequestConfiguration : IEntityTypeConfiguration<Request>
    {
        public void Configure(EntityTypeBuilder<Request> builder)
        {
            builder.HasKey(r => r.Id);

            // Requête fréquente dans SyncRequestStatusesAsync
            builder.HasIndex(r => new { r.UserId, r.Status })
                .HasDatabaseName("idx_requests_userid_status");
        }
    }
}
