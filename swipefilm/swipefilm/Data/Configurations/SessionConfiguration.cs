using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using swipefilm.Models;

namespace swipefilm.Data.Configurations
{
    public class SessionConfiguration : IEntityTypeConfiguration<Session>
    {
        public void Configure(EntityTypeBuilder<Session> builder)
        {
            builder.HasKey(s => s.Id);
            builder.HasIndex(s => s.Code).IsUnique().HasDatabaseName("idx_sessions_code");

            builder.HasOne(s => s.CreatedBy)
                .WithMany(u => u.CreatedSessions)
                .HasForeignKey(s => s.CreatedByUserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
