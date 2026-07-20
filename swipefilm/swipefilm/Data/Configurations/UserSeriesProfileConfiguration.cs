using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using swipefilm.Models;

namespace swipefilm.Data.Configurations
{
    public class UserSeriesProfileConfiguration : IEntityTypeConfiguration<UserSeriesProfile>
    {
        public void Configure(EntityTypeBuilder<UserSeriesProfile> builder)
        {
            builder.HasKey(p => p.Id);

            builder.HasIndex(p => p.UserId)
                .IsUnique()
                .HasDatabaseName("idx_userseriesprofiles_userid");

            builder.Property(p => p.GenreWeights).HasColumnType("jsonb");
            builder.Property(p => p.CreatorWeights).HasColumnType("jsonb");
            builder.Property(p => p.ActorWeights).HasColumnType("jsonb");
            builder.Property(p => p.KeywordWeights).HasColumnType("jsonb");
            builder.Property(p => p.OriginalLanguageWeights).HasColumnType("jsonb");
            builder.Property(p => p.PreferredDecadeWeights).HasColumnType("jsonb");
            builder.Property(p => p.GenreCounts).HasColumnType("jsonb");
            builder.Property(p => p.CreatorCounts).HasColumnType("jsonb");
            builder.Property(p => p.ActorCounts).HasColumnType("jsonb");
            builder.Property(p => p.KeywordCounts).HasColumnType("jsonb");
            builder.Property(p => p.LanguageCounts).HasColumnType("jsonb");

            builder.HasOne(p => p.User)
                .WithOne(u => u.SeriesProfile)
                .HasForeignKey<UserSeriesProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
