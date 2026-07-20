// SwipeFilm.Infrastructure/Data/AppDbContext.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using swipefilm.Models;

public class AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ServerConfig> ServerConfig => Set<ServerConfig>();
    public DbSet<UserSeerr> UserSeerr => Set<UserSeerr>();
    public DbSet<UserRadarr> UserRadarr => Set<UserRadarr>();
    public DbSet<UserSonarr> UserSonarr => Set<UserSonarr>();
    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<WatchHistory> WatchHistory => Set<WatchHistory>();
    public DbSet<Swipe> Swipes => Set<Swipe>();
    public DbSet<Watchlist> Watchlists => Set<Watchlist>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<SessionMember> SessionMembers => Set<SessionMember>();
    public DbSet<SessionMatch> SessionMatches => Set<SessionMatch>();
    // ✅ Remplace l'ancien Request par MediaRequest
    public DbSet<MediaRequest> MediaRequests => Set<MediaRequest>();
    public DbSet<MediaRequestSeason> MediaRequestSeasons => Set<MediaRequestSeason>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<ServerMovie> ServerMovie => Set<ServerMovie>();
    public DbSet<DiscoveryCache> DiscoveryCaches => Set<DiscoveryCache>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();

    // ─── Séries ─────────────────────────────────────────────────
    public DbSet<Series> Series => Set<Series>();
    public DbSet<SeriesSeason> SeriesSeasons => Set<SeriesSeason>();
    public DbSet<ServerSeries> ServerSeries => Set<ServerSeries>();
    public DbSet<ServerSeriesSeason> ServerSeriesSeasons => Set<ServerSeriesSeason>();
    public DbSet<SeriesWatchHistory> SeriesWatchHistory => Set<SeriesWatchHistory>();
    public DbSet<UserSeriesProfile> UserSeriesProfiles => Set<UserSeriesProfile>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // ← Important pour Identity
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
                if (entry.Properties.Any(p => p.Metadata.Name == "CreatedAt"))
                    entry.Property("CreatedAt").CurrentValue = DateTime.UtcNow;
        }
        return base.SaveChangesAsync(ct);
    }
}