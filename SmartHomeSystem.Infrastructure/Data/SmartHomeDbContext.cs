using Microsoft.EntityFrameworkCore;
using SmartHomeSystem.Domain.Entities;

namespace SmartHomeSystem.Infrastructure.Data;

/// <summary>
/// Контекст бази даних. Доменні сутності не знають про EF Core: усе відображення
/// (таблиці, ключі, зв'язки) описано в класах Data/Configurations.
/// </summary>
public sealed class SmartHomeDbContext : DbContext
{
    public SmartHomeDbContext(DbContextOptions<SmartHomeDbContext> options) : base(options)
    {
    }

    public DbSet<SmartHome> Homes => Set<SmartHome>();

    public DbSet<SmartRoom> Rooms => Set<SmartRoom>();

    public DbSet<HomeDevice> Devices => Set<HomeDevice>();

    public DbSet<RoomTelemetry> Telemetry => Set<RoomTelemetry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(SmartHomeDbContext).Assembly);
}
