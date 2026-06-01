// SwipeFilm.Infrastructure/Data/AppDbContext.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using swipefilm.Models;

public class AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UserServer> UserServers => Set<UserServer>();
    public DbSet<UserSeerr> UserSeerr => Set<UserSeerr>();
    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<WatchHistory> WatchHistory => Set<WatchHistory>();
    public DbSet<Swipe> Swipes => Set<Swipe>();
    public DbSet<Watchlist> Watchlists => Set<Watchlist>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<SessionMember> SessionMembers => Set<SessionMember>();
    public DbSet<SessionMatch> SessionMatches => Set<SessionMatch>();
    public DbSet<Request> Requests => Set<Request>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

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