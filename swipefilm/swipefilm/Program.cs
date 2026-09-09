using System.Security.Claims;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Serilog;
using Serilog.Formatting.Json;
using swipefilm.Auth;
using swipefilm.Auth.swipefilm.Services;
using swipefilm.Data;

// ✅ Charge .env (jamais commité) dans les variables d'environnement — les
// clés/mots de passe sortent de appsettings.json. IConfiguration lit déjà
// les variables d'environnement par défaut (format Section__Cle), donc rien
// d'autre à câbler : ConnectionStrings__DefaultConnection,
// Encryption__Secret, Tmdb__ApiKey. (Les tokens webhook Radarr/Sonarr ne
// sont plus des secrets .env — générés et gérés par l'app elle-même,
// voir AppConfigService.GetOrCreate{Radarr,Sonarr}WebhookTokenAsync.)
// ✅ Optionnel — en Docker il n'y a pas de fichier .env dans l'image (les
// secrets arrivent déjà comme vraies variables d'env via docker-compose),
// Env.Load() planterait sinon en cherchant un fichier qui n'existe pas.
if (File.Exists(".env"))
    DotNetEnv.Env.Load();

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

// ─── Auth par cookie de session ─────────────────────────────────
// ✅ Même principe qu'Overseerr : le cookie ne contient qu'un id de session
// opaque (claim "sid"), la table AuthSessions est la seule source de
// vérité — révocable à tout moment (logout), contrairement à un JWT qui
// reste valide jusqu'à expiration même après une "déconnexion" client-only.
// Possible uniquement parce que frontend et backend sont désormais servis
// depuis la même origine (voir Program.cs plus bas, UseStaticFiles) — pas
// besoin de SameSite=None/cross-site.
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.Cookie.Name = "swipefilm_session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;

    options.Events = new CookieAuthenticationEvents
    {
        // ✅ Vérifie la session à chaque requête contre la base (un logout ou
        // une session supprimée prend effet immédiatement, pas seulement à
        // l'expiration du cookie) ET reconstruit les claims depuis l'utilisateur
        // actuel — permissions toujours à jour, contrairement au JWT qui
        // restait valide jusqu'à 7 jours même après un changement de rôle.
        OnValidatePrincipal = async context =>
        {
            var sid = context.Principal?.FindFirstValue("sid");
            if (sid is null || !Guid.TryParse(sid, out var sessionId))
            {
                context.RejectPrincipal();
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var session = await db.AuthSessions.FindAsync(sessionId);
            if (session is null || session.ExpiresAt < DateTime.UtcNow)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            var user = await db.Users.FindAsync(session.UserId);
            if (user is null)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email!),
                new Claim(ClaimTypes.Name, user.DisplayName),
                new Claim("permissions", user.Permissions.ToString()),
                new Claim("isAdmin", user.IsAdmin.ToString().ToLower()),
                new Claim("sid", sid),
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            context.ReplacePrincipal(new ClaimsPrincipal(identity));
        },
        // ✅ API JSON — jamais de redirection HTML vers une page de login
        // (comportement par défaut du handler cookie, pensé pour Razor),
        // juste un code de statut comme avec le Bearer JWT avant.
        OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        },
        OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        },
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
// ✅ Auth par cookie de session — Swagger UI envoie déjà le cookie du
// navigateur automatiquement (même origine), pas de schéma Bearer à
// déclarer ici comme avant avec le JWT.
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "SwipeFilm API", Version = "v1" });
});

// ─── Controllers + SignalR ────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddSignalR();

// ─── Services ─────────────────────────────────────────────────
builder.Services.AddScoped<AuthManager>();
builder.Services.AddScoped<IEncryptionService, EncryptionService>();
// ✅ Singleton — charge config/settings.json une seule fois en mémoire au
// démarrage, pas à chaque requête (contrairement aux services Scoped).
builder.Services.AddSingleton<IAppConfigService, AppConfigService>();
builder.Services.AddScoped<JellyfinService>();
builder.Services.AddScoped<PlexService>();
builder.Services.AddScoped<SyncService>();
builder.Services.AddScoped<TmdbService>();
builder.Services.AddScoped<RecommendationEngine>();
builder.Services.AddScoped<SessionService>();
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

// ✅ Pas de UseHttpsRedirection — comme Radarr/Sonarr/Overseerr, cette appli
// ne termine pas TLS elle-même (http seul, en dev comme en Docker). Pour du
// HTTPS, mettre un reverse proxy (Nginx/Caddy/Traefik) devant avec un vrai certificat.
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

// ─── Frontend statique (export Next.js) ────────────────────────
// ✅ Même pattern que Radarr/Sonarr : le backend sert directement les
// fichiers du frontend (wwwroot, peuplé au build Docker depuis out/ —
// voir Dockerfile) — pas de serveur Node séparé en prod.
app.UseDefaultFiles();
app.UseStaticFiles();

// ─── Routes ───────────────────────────────────────────────────
app.MapControllers();
app.MapHub<swipefilm.Hubs.SessionHub>("/hubs/session");

// ✅ Tout ce qui ne matche ni un contrôleur ni un fichier statique existant
// est une route du frontend (SPA côté client une fois le JS chargé) — sert
// le fichier .html correspondant. /movie/* et /series/* partagent un seul
// shell ("_") : generateStaticParams côté Next n'en génère qu'un, le vrai
// id est lu dans l'URL par useParams() au chargement, pas au build.
app.MapFallback(async context =>
{
    var webRoot = app.Environment.WebRootPath;
    var path = context.Request.Path.Value ?? "/";

    string file;
    if (path.StartsWith("/movie", StringComparison.OrdinalIgnoreCase))
        file = "movie/_.html";
    else if (path.StartsWith("/series", StringComparison.OrdinalIgnoreCase))
        file = "series/_.html";
    else if (path.StartsWith("/session", StringComparison.OrdinalIgnoreCase))
        file = "session/_.html";
    else if (path.StartsWith("/join", StringComparison.OrdinalIgnoreCase))
        file = "join/_.html";
    else
    {
        var trimmed = path.Trim('/');
        var candidate = string.IsNullOrEmpty(trimmed) ? "index.html" : $"{trimmed}.html";
        file = File.Exists(Path.Combine(webRoot, candidate)) ? candidate : "index.html";
    }

    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(Path.Combine(webRoot, file));
});

app.Run();