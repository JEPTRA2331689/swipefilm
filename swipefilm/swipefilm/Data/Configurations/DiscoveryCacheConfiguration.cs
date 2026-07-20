// Configurations/DiscoveryCacheConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using swipefilm.Models;
using System.Text.Json;

public class DiscoveryCacheConfiguration : IEntityTypeConfiguration<DiscoveryCache>
{
    public void Configure(EntityTypeBuilder<DiscoveryCache> builder)
    {
        builder.HasKey(d => d.Id);

        // Index composite — un cache par user+section
        builder.HasIndex(d => new { d.UserId, d.SectionId })
            .IsUnique()
            .HasDatabaseName("idx_discoverycache_user_section");

       
    }
}