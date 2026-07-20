using System.Text;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Serilog;
using Serilog.Formatting.Json;
using swipefilm.Auth;
using swipefilm.Auth.swipefilm.Services;
using swipefilm.Data;

var builder = WebApplication.CreateBuilder(args);

// ✅ Un fichier par jour, JSON (une ligne = un événement) — lu tel quel par
// GET /api/logs, pas de parsing de format texte fragile. Même principe que
// Winston chez Overseerr (transport "machinelogs").
var logsDirectory = Path.Combine(builder.Environment.ContentRootPath, "logs");
builder.Host.UseSerilog((context, configuration) => configuration
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        new JsonFormatter(renderMessage: true),
        Path.Combine(logsDirectory, "swipefilm-.json"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        fileSizeLimitBytes: 20 * 1024 * 1024));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// ─── PostgreSQL + EF ──────────────────────────────────────────
var dataSourceBuilder = new NpgsqlDataSourceBuilder(
    builder.Configuration.GetConnectionString("DefaultConnection"));
dataSourceBuilder.EnableDynamicJson();
var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(dataSource,
        npgsql => npgsql.MigrationsAssembly("swipefilm")));

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseNpgsql(dataSource,
        npgsql => npgsql.MigrationsAssembly("swipefilm")),ServiceLifetime.Scoped);

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
builder.Services.AddScoped<IServerConfigService, ServerConfigService>();
builder.Services.AddScoped<JellyfinService>();
builder.Services.AddScoped<PlexService>();
builder.Services.AddScoped<SyncService>();
builder.Services.AddScoped<TmdbService>();
builder.Services.AddScoped<RecommendationEngine>();
builder.Services.AddScoped<PersonalizedDiscoveryService>();
builder.Services.AddScoped<SyncBackgroundJobService>();
builder.Services.AddScoped<RadarrService>();
builder.Services.AddScoped<SonarrService>();
builder.Services.AddScoped<MediaRequestService>();
builder.Services.AddMemoryCache();


// ─── HttpClients ──────────────────────────────────────────────
builder.Services.AddHttpClient("Jellyfin");
builder.Services.AddHttpClient("Plex");
builder.Services.AddHttpClient("Tmdb");
builder.Services.AddHttpClient("Seerr");
builder.Services.AddHttpClient("Radarr");
builder.Services.AddHttpClient("Sonarr");

// ════════════════════════════════════════════════════════════════
var app = builder.Build();
// ════════════════════════════════════════════════════════════════

// ─── Auto-migration ───────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider
        .GetRequiredService<RoleManager<IdentityRole<Guid>>>();

    foreach (var role in new[] { "Admin", "User" })
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole<Guid>(role));
    }
}

// ─── Pipeline ─────────────────────────────────────────────────
app.UseSerilogRequestLogging(); // ✅ un log par requête HTTP (méthode, chemin, code, durée)

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ✅ Exclu /api/webhooks/* — Radarr/Sonarr n'ont pas forcément un certificat
// HTTPS de confiance vers ce serveur, la redirection les renverrait droit
// dans le même mur SSL. Le token en query string reste la sécurité de ces routes.
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments("/api/webhooks"),
    branch => branch.UseHttpsRedirection());
app.UseCors("AllowAll"); // ← ajoute ici
app.UseAuthentication(); // ← doit être avant UseAuthorization
app.UseAuthorization();

// ─── Hangfire Dashboard ───────────────────────────────────────
// ✅ Doit être APRÈS builder.Build()
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireAuthFilter()]
});

// ─── Jobs planifiés ───────────────────────────────────────────
// ✅ Toutes les 5 min — c'est aussi ce qui confirme la vraie disponibilité
// (ServerMovie/ServerSeries) des requêtes en Downloading, voir SyncSingleServerAsync
RecurringJob.AddOrUpdate<SyncBackgroundJobService>(
    "sync-all-servers",
    x => x.SyncAllServersAsync(),
    "*/5 * * * *",
    new RecurringJobOptions { QueueName = "default" });

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

// ✅ Filet de sécurité derrière les webhooks Radarr/Sonarr (toutes les 30 min)
RecurringJob.AddOrUpdate<SyncBackgroundJobService>(
    "sync-request-statuses",
    x => x.SyncAllRequestStatusesAsync(),
    "*/30 * * * *",
    new RecurringJobOptions { QueueName = "default" });

// ─── Routes ───────────────────────────────────────────────────
app.MapControllers();

app.Run();