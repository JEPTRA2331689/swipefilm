using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using swipefilm.Models;

namespace swipefilm.Data.Configurations
{
    public class ServerConfigConfiguration : IEntityTypeConfiguration<ServerConfig>
    {
        public void Configure(EntityTypeBuilder<ServerConfig> builder)
        {
            builder.HasKey(s => s.Id);
        }
    }
}
