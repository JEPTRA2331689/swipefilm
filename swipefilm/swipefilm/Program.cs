using System.Text;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using swipefilm.Auth;
using swipefilm.Auth.swipefilm.Services;
using swipefilm.Data;

var builder = WebApplication.CreateBuilder(args);

// ─── PostgreSQL + EF ──────────────────────────────────────────
var dataSourceBuilder = new NpgsqlDataSourceBuilder(
    builder.Configuration.GetConnectionString("DefaultConnection"));
dataSourceBuilder.EnableDynamicJson();
var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(dataSource,
        npgsql => npgsql.MigrationsAssembly("swipefilm")));

// ─── Identity ─────────────────────────────────────────────────
builder.Services.AddIdentity<User, IdentityRole<Guid>>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// ─── JWT ──────────────────────────────────────────────────────
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!))
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var token = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(token) && path.StartsWithSegments("/hubs"))
                context.Token = token;
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// ─── Hangfire ─────────────────────────────────────────────────
// ✅ Doit être AVANT builder.Build()
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options =>
        options.UseNpgsqlConnection(
            builder.Configuration.GetConnectionString("DefaultConnection")))); // ← même clé que EF

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 2;
    options.Queues = ["critical", "default", "low"];
});

// ─── Swagger ──────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "SwipeFilm API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Entre ton token JWT ici"
    });
    c.AddSecurityRequirement(new()
    {
        {
            new() { Reference = new() {
                Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                Id   = "Bearer" } },
            []
        }
    });
});

// ─── Controllers + SignalR ────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddSignalR();

// ─── Services ─────────────────────────────────────────────────
builder.Services.AddScoped<AuthManager>();
builder.Services.AddScoped<IEncryptionService, EncryptionService>();
builder.Services.AddScoped<IUserServerService, UserServerService>();
builder.Services.AddScoped<JellyfinService>();
builder.Services.AddScoped<PlexService>();
builder.Services.AddScoped<SyncService>();
builder.Services.AddScoped<TmdbService>();
builder.Services.AddScoped<RecommendationEngine>();
builder.Services.AddScoped<SeerrService>();
builder.Services.AddScoped<PersonalizedDiscoveryService>();
builder.Services.AddScoped<SyncBackgroundJobService>();

// ─── HttpClients ──────────────────────────────────────────────
builder.Services.AddHttpClient("Jellyfin");
builder.Services.AddHttpClient("Plex");
builder.Services.AddHttpClient("Tmdb");
builder.Services.AddHttpClient("Seerr");

// ════════════════════════════════════════════════════════════════
var app = builder.Build();
// ════════════════════════════════════════════════════════════════

// ─── Auto-migration ───────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// ─── Pipeline ─────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication(); // ← doit être avant UseAuthorization
app.UseAuthorization();

// ─── Hangfire Dashboard ───────────────────────────────────────
// ✅ Doit être APRÈS builder.Build()
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireAuthFilter()]
});

// ─── Jobs planifiés ───────────────────────────────────────────
RecurringJob.AddOrUpdate<SyncBackgroundJobService>(
    "sync-all-servers",
    "default",
    x => x.SyncAllServersAsync(),
    Cron.Hourly);

RecurringJob.AddOrUpdate<TmdbService>(
    "enrich-movies",
    "low",
    x => x.EnrichAllMoviesAsync(),
    Cron.Daily);

RecurringJob.AddOrUpdate<SyncBackgroundJobService>(
    "discovery-all-users",
    "low",
    x => x.DiscoverForAllUsersAsync(),
    "0 3 * * *");

// ─── Routes ───────────────────────────────────────────────────
app.MapControllers();

app.Run();