// swipefilm/Controllers/LogsController.cs
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using swipefilm.Auth;
using swipefilm.Models;

namespace swipefilm.Controllers
{
    /// <summary>
    /// Lit les fichiers de logs Serilog (voir Program.cs — un fichier JSON par
    /// jour, rotation + rétention 7 jours) — même principe que le viewer de
    /// logs d'Overseerr (pagination, filtre niveau, recherche texte).
    /// </summary>
    [ApiController]
    [Route("api/logs")]
    [Authorize]
    [RequirePermission(Permission.Admin)]
    public class LogsController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        public LogsController(IWebHostEnvironment env)
        {
            _env = env;
        }

        public record LogEntryDto(DateTime Timestamp, string Level, string? SourceContext, string Message);

        [HttpGet]
        public IActionResult GetLogs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 25,
            [FromQuery] string? level = null,
            [FromQuery] string? search = null)
        {
            var logsDirectory = Path.Combine(_env.ContentRootPath, "logs");
            if (!Directory.Exists(logsDirectory))
                return Ok(new { total = 0, page, pageSize, results = Array.Empty<LogEntryDto>() });

            // ✅ Les noms de fichiers (swipefilm-20260718.json) trient déjà par
            // date — le plus récent en premier, pas besoin de re-trier par
            // timestamp après coup.
            var files = Directory.GetFiles(logsDirectory, "swipefilm-*.json")
                .OrderByDescending(f => f)
                .ToList();

            var entries = new List<LogEntryDto>();

            foreach (var file in files)
            {
                string[] lines;
                try { lines = System.IO.File.ReadAllLines(file); }
                catch (IOException) { continue; } // fichier du jour en cours de rotation

                for (var i = lines.Length - 1; i >= 0; i--)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;

                    LogEntryDto entry;
                    try
                    {
                        using var doc = JsonDocument.Parse(lines[i]);
                        var root = doc.RootElement;

                        var timestamp = root.TryGetProperty("Timestamp", out var ts)
                            ? ts.GetDateTime() : DateTime.MinValue;
                        var entryLevel = root.TryGetProperty("Level", out var lvl)
                            ? lvl.GetString() ?? "Information" : "Information";
                        var sourceContext = root.TryGetProperty("Properties", out var props)
                            && props.TryGetProperty("SourceContext", out var sc)
                            ? sc.GetString() : null;
                        var message = root.TryGetProperty("RenderedMessage", out var msg)
                            ? msg.GetString() ?? "" : "";

                        entry = new LogEntryDto(timestamp, entryLevel, sourceContext, message);
                    }
                    catch (JsonException) { continue; } // ligne malformée/tronquée, ignorée

                    if (level is not null && !string.Equals(entry.Level, level, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (search is not null && !entry.Message.Contains(search, StringComparison.OrdinalIgnoreCase))
                        continue;

                    entries.Add(entry);
                }
            }

            var total = entries.Count;
            var results = entries.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return Ok(new { total, page, pageSize, results });
        }
    }
}
