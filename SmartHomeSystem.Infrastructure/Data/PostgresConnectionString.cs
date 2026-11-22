using Npgsql;

namespace SmartHomeSystem.Infrastructure.Data;

/// <summary>
/// Render (і більшість хмарних платформ) видає рядок підключення до PostgreSQL у вигляді URL:
/// postgres://user:password@host:port/dbname. Npgsql очікує формат «Host=...;Port=...;...».
/// Цей клас приймає обидва варіанти, тому у змінну середовища
/// ConnectionStrings__SmartHomeDatabase можна вставляти значення з Render без змін.
/// </summary>
public static class PostgresConnectionString
{
    public static string Normalize(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Рядок підключення порожній.", nameof(connectionString));
        }

        var value = connectionString.Trim();
        var isUrl = value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
                    || value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);

        if (!isUrl)
        {
            return value; // уже у форматі Npgsql
        }

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,

            // Внутрішня адреса Render працює без TLS, зовнішня — з TLS; Prefer підходить для обох випадків.
            SslMode = SslMode.Prefer
        };

        return builder.ConnectionString;
    }
}