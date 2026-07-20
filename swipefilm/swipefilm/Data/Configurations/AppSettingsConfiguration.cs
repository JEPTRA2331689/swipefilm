// swipefilm/Data/Configurations/AppSettingsConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using swipefilm.Models;

namespace swipefilm.Data.Configurations
{
    public class AppSettingsConfiguration : IEntityTypeConfiguration<AppSettings>
    {
        public void Configure(EntityTypeBuilder<AppSettings> builder)
        {
            builder.HasKey(a => a.Key);
            builder.Property(a => a.Key).HasMaxLength(100);
            builder.Property(a => a.Value).HasMaxLength(2000);

            // Seed — app non configurée par défaut
            builder.HasData(new AppSettings
            {
                Key = "IsSetupComplete",
                Value = "false"
            });
        }
    }
}