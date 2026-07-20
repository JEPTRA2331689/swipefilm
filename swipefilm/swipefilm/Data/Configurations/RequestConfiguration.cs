using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using swipefilm.Models;

namespace swipefilm.Data.Configurations
{
    // ✅ Renommé en MediaRequestConfiguration pour éviter le conflit avec Models.MediaRequest
    public class MediaRequestConfiguration : IEntityTypeConfiguration<MediaRequest>
    {
        public void Configure(EntityTypeBuilder<MediaRequest> builder) // ✅ MediaRequest
        {
            builder.HasKey(r => r.Id);

            // ✅ Index pour SyncRequestStatusesAsync — requête fréquente
            builder.HasIndex(r => new { r.UserId, r.Status })
                .HasDatabaseName("idx_requests_userid_status");

            // ✅ Index pour GetMovieRequestStatus — cherche par film
            builder.HasIndex(r => new { r.MovieId, r.Status })
                .HasDatabaseName("idx_requests_movieid_status");

            // ✅ Index équivalent pour les requêtes de séries
            builder.HasIndex(r => new { r.SeriesId, r.Status })
                .HasDatabaseName("idx_requests_seriesid_status");

            // ✅ Relations
            builder.HasOne(r => r.User)
                .WithMany(u => u.Requests)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(r => r.Movie)
                .WithMany(m => m.Requests)
                .HasForeignKey(r => r.MovieId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(r => r.Series)
                .WithMany(s => s.Requests)
                .HasForeignKey(r => r.SeriesId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(r => r.ProcessedBy)
                .WithMany()
                .HasForeignKey(r => r.ProcessedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            // ✅ Champs optionnels
            builder.Property(r => r.DeclineReason).HasMaxLength(500);
            builder.Property(r => r.Message).HasMaxLength(1000);

            // Exactement un des deux doit être renseigné, jamais les deux ni aucun
            builder.ToTable(t => t.HasCheckConstraint(
                "ck_mediarequests_movie_xor_series",
                "(\"MovieId\" IS NOT NULL) <> (\"SeriesId\" IS NOT NULL)"));
        }
    }

    public class MediaRequestSeasonConfiguration : IEntityTypeConfiguration<MediaRequestSeason>
    {
        public void Configure(EntityTypeBuilder<MediaRequestSeason> builder)
        {
            builder.HasKey(s => s.Id);

            builder.HasIndex(s => new { s.MediaRequestId, s.SeasonNumber })
                .IsUnique()
                .HasDatabaseName("idx_mediarequestseasons_requestid_seasonnumber");

            builder.HasOne(s => s.MediaRequest)
                .WithMany(r => r.Seasons)
                .HasForeignKey(s => s.MediaRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}