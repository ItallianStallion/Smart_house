using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Console;
using SmartHomeSystem.API.Health;
using SmartHomeSystem.API.Middleware;
using SmartHomeSystem.API.Validators;
using SmartHomeSystem.Domain.Interfaces;
using SmartHomeSystem.Infrastructure.Data;
using SmartHomeSystem.Infrastructure.Repositories;
using SmartHomeSystem.Infrastructure.Seeding;
using SmartHomeSystem.Services.Interfaces;
using SmartHomeSystem.Services.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- Конфігурація ----------
// Джерела за пріоритетом (пізніше джерело перекриває раніше):
// appsettings.json -> appsettings.{Environment}.json -> user-secrets (лише Development) -> змінні середовища -> аргументи командного рядка.
// Тому один і той самий код працює локально, в Docker і в хмарі без перекомпіляції.
var connectionString = builder.Configuration.GetConnectionString("SmartHomeDatabase")
    ?? throw new InvalidOperationException(
        "Не налаштовано рядок підключення ConnectionStrings:SmartHomeDatabase " +
        "(appsettings.json, user-secrets або змінна середовища ConnectionStrings__SmartHomeDatabase).");

// JSON-журнал (Production): не екранувати кирилицю, щоб повідомлення було читабельним.
builder.Services.Configure<JsonConsoleFormatterOptions>(options =>
    options.JsonWriterOptions = new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

// Клієнт Lab2 надсилає snake_case JSON і рядкові значення enum.
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Swagger (OpenAPI): інтерактивна документація на /swagger, описи беруться з XML-коментарів контролерів.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// ---------- Валідація запитів (FluentValidation) ----------
builder.Services.AddValidatorsFromAssemblyContaining<CreateDeviceRequestValidator>();

// ---------- Постійне сховище: EF Core + PostgreSQL ----------
// Рядок підключення можна задати і як URL (postgres://user:pass@host:port/db — так його видає Render),
// і у форматі Npgsql (Host=...;Database=...). Normalize приводить обидва варіанти до формату Npgsql.
var npgsqlConnectionString = PostgresConnectionString.Normalize(connectionString);

builder.Services.AddDbContext<SmartHomeDbContext>(options =>
{
    options.UseNpgsql(npgsqlConnectionString);

    // Лише для розробки: у журнал SQL потрапляють значення параметрів (дані користувачів), тому за замовчуванням вимкнено.
    if (builder.Configuration.GetValue("Database:LogSensitiveData", false))
    {
        options.EnableSensitiveDataLogging().EnableDetailedErrors();
    }
});

// Dependency Injection: «коли потрібен IRoomRepository — дай EfRoomRepository».
// Scoped (а не Singleton, як InMemory): репозиторій працює через DbContext, який живе один HTTP-запит.
builder.Services.AddScoped<IHomeRepository, EfHomeRepository>();
builder.Services.AddScoped<IRoomRepository, EfRoomRepository>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<IClimateControlService, ClimateControlService>();
builder.Services.AddScoped<ITelemetryService, TelemetryService>();

// ---------- Health checks ----------
// "ready" — перевірки залежностей (база даних); перевірка без тегів нижче — лише «процес живий».
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SmartHomeDbContext>("database", tags: new[] { "ready" });

// ---------- CORS ----------
// Фронтенд зазвичай віддається з того самого сервісу (wwwroot), тому CORS не потрібен. Якщо ж фронтенд розміщено
// окремим сервісом, додайте його адресу в змінну Cors__AllowedOrigins (кілька адрес — через кому).
var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        }
    });
});

// ---------- HTTPS за reverse proxy ----------
// У хмарі TLS зазвичай завершує платформа (Ingress / App Service). Довіряємо її заголовкам X-Forwarded-*,
// щоб застосунок знав початкову схему (https) і міг коректно робити редірект.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();
var logger = app.Logger;

logger.LogInformation(
    "Запуск SmartHomeSystem.API у середовищі {Environment}",
    app.Environment.EnvironmentName);

// ---------- Ініціалізація бази ----------
using (var scope = app.Services.CreateScope())
{
    try
    {
        if (app.Configuration.GetValue("Database:AutoMigrate", true))
        {
            var db = scope.ServiceProvider.GetRequiredService<SmartHomeDbContext>();
            var migrated = await DatabaseInitializer.ApplySchemaAsync(db);
            logger.LogInformation(
                "Схему бази даних підготовлено ({Mode})",
                migrated ? "міграції" : "EnsureCreated");
        }

        if (app.Configuration.GetValue("Database:SeedDemoData", false))
        {
            var homes = scope.ServiceProvider.GetRequiredService<IHomeRepository>();
            var rooms = scope.ServiceProvider.GetRequiredService<IRoomRepository>();

            // Дані зберігаються між запусками, тому наповнюємо лише порожню базу.
            if (!(await rooms.GetAllAsync()).Any())
            {
                await DemoDataSeeder.SeedAsync(homes, rooms);
                logger.LogInformation("Базу наповнено тестовими даними");
            }
            else
            {
                logger.LogInformation("У базі вже є кімнати — тестові дані не додавались");
            }
        }
    }
    catch (Exception ex)
    {
        // Рядок підключення в журнал не потрапляє — лише факт і виняток.
        logger.LogCritical(ex, "Не вдалося ініціалізувати базу даних");
        throw;
    }
}

// ---------- Конвеєр HTTP-запитів ----------
app.UseForwardedHeaders();

// Централізована обробка помилок: перший у конвеєрі, щоб перехоплювати винятки всіх наступних компонентів.
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Configuration.GetValue("Https:Redirect", false))
{
    app.UseHttpsRedirection();
}

app.UseCors("Frontend");

// Фронтенд (SPA без збірки): wwwroot/index.html віддається на кореневому маршруті "/".
app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue("Swagger:Enabled", false))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

// /health — «застосунок живий» (процес відповідає). Не перевіряє залежності, тому не падає через БД.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthResponseWriter.WriteAsync
});

// /health/ready — «готовий приймати запити»: додатково перевіряє доступність бази даних (503, якщо недоступна).
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthResponseWriter.WriteAsync
});

app.Run();
