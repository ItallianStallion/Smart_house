using Microsoft.EntityFrameworkCore;

namespace SmartHomeSystem.Infrastructure.Data;

public static class DatabaseInitializer
{
    /// <summary>
    /// Готує схему бази: якщо в проєкті є міграції (dotnet ef migrations add ...), застосовує їх;
    /// якщо міграцій ще немає — створює таблиці напряму (зручно для першого запуску й тестів).
    /// </summary>
    /// <returns>true, якщо застосовувались міграції; false, якщо схему створено без них.</returns>
    public static async Task<bool> ApplySchemaAsync(SmartHomeDbContext db, CancellationToken ct = default)
    {
        if (db.Database.GetMigrations().Any())
        {
            await db.Database.MigrateAsync(ct);
            return true;
        }

        await db.Database.EnsureCreatedAsync(ct);
        return false;
    }
}
