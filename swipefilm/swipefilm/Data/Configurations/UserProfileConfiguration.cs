using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using swipefilm.Models;

namespace swipefilm.Data.Configurations
{
    public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
    {
        public void Configure(EntityTypeBuilder<UserProfile> builder)
        {
            builder.HasKey(p => p.Id);

            builder.HasIndex(p => p.UserId)
                .IsUnique()
                .HasDatabaseName("idx_userprofiles_userid");

            builder.Property(p => p.GenreWeights)
                .HasColumnType("jsonb");
            builder.Property(p => p.DirectorWeights)
                .HasColumnType("jsonb");
            builder.Property(p => p.ActorWeights)
                .HasColumnType("jsonb");
            builder.Property(p => p.KeywordWeights)
                .HasColumnType("jsonb");

            builder.HasOne(p => p.User)
                .WithOne(u => u.Profile)
                .HasForeignKey<UserProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
