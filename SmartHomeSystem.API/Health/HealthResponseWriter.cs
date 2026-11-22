using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SmartHomeSystem.API.Health;

/// <summary>
/// Короткий JSON-звіт про стан. Навмисно не містить повідомлень винятків, рядків підключення
/// та шляхів до файлів — лише назви перевірок і їхній статус.
/// </summary>
public static class HealthResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString()
            }),
            duration_ms = (long)report.TotalDuration.TotalMilliseconds
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
