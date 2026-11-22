using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartHomeSystem.Domain.Entities;

namespace SmartHomeSystem.Infrastructure.Data.Configurations;

// Правила цілісності задано і в застосунку (доменні сутності, валідатори), і в самій базі даних
// (NOT NULL, довжина, CHECK, UNIQUE, зовнішні ключі): база захищає дані, навіть якщо їх змінюють поза API.
// Ключі Guid генерує сама сутність (Guid.NewGuid()), тому ValueGeneratedNever:
// інакше EF вважав би нову дочірню сутність з уже заданим Id «зміненою», а не «доданою».

public sealed class SmartHomeConfiguration : IEntityTypeConfiguration<SmartHome>
{
    public void Configure(EntityTypeBuilder<SmartHome> builder)
    {
        builder.ToTable("Homes");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();
        builder.Property(h => h.Name).IsRequired().HasMaxLength(100);
        builder.Property(h => h.Address).HasMaxLength(200);
    }
}

public sealed class SmartRoomConfiguration : IEntityTypeConfiguration<SmartRoom>
{
    public void Configure(EntityTypeBuilder<SmartRoom> builder)
    {
        builder.ToTable("Rooms", table =>
        {
            table.HasCheckConstraint("CK_Rooms_Name_Length", "length(\"Name\") >= 3");
            table.HasCheckConstraint("CK_Rooms_Floor", "\"Floor\" BETWEEN 0 AND 10");
            table.HasCheckConstraint("CK_Rooms_TargetTemperature", "\"TargetTemperature\" BETWEEN 10 AND 30");
        });
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Name).IsRequired().HasMaxLength(100);

        // Переліки зберігаються текстом, щоб значення в БД було читабельним.
        builder.Property(r => r.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(r => r.CurrentStatus).HasConversion<string>().HasMaxLength(30);

        // PostgreSQL (timestamp with time zone) приймає лише DateTime з Kind = Utc, тому при записі й читанні
        // примусово задаємо Utc (JSON також отримує суфікс «Z»).
        builder.Property(r => r.LastSync)
            .HasConversion(v => DateTime.SpecifyKind(v, DateTimeKind.Utc), v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        builder.HasOne<SmartHome>().WithMany().HasForeignKey(r => r.HomeId).OnDelete(DeleteBehavior.Cascade);

        // Колекції в сутності лише для читання (_devices, _history) — EF працює з ними напряму через поля.
        builder.HasMany(r => r.Devices).WithOne().HasForeignKey(d => d.RoomId).IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Devices).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(r => r.History).WithOne().HasForeignKey(t => t.RoomId).IsRequired().OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.History).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Назва кімнати унікальна в межах будинку. PostgreSQL порівнює рядки з урахуванням регістру, тому
        // нечутливість до регістру перевіряє сервіс; унікальний індекс — остання лінія захисту.
        builder.HasIndex(r => new { r.HomeId, r.Name }).IsUnique();

        builder.Ignore(r => r.LastTelemetry); // обчислювана властивість
    }
}

public sealed class HomeDeviceConfiguration : IEntityTypeConfiguration<HomeDevice>
{
    public void Configure(EntityTypeBuilder<HomeDevice> builder)
    {
        builder.ToTable("Devices", table =>
            table.HasCheckConstraint("CK_Devices_Name_Length", "length(\"Name\") >= 2"));
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.Property(d => d.Name).IsRequired().HasMaxLength(100);
        builder.Property(d => d.Type).HasConversion<string>().HasMaxLength(30);

        // Час зберігається в UTC (Npgsql вимагає Kind = Utc при записі).
        builder.Property(d => d.LastUpdated)
            .HasConversion(v => DateTime.SpecifyKind(v, DateTimeKind.Utc), v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
    }
}

public sealed class RoomTelemetryConfiguration : IEntityTypeConfiguration<RoomTelemetry>
{
    public void Configure(EntityTypeBuilder<RoomTelemetry> builder)
    {
        builder.ToTable("Telemetry", table =>
        {
            table.HasCheckConstraint("CK_Telemetry_Temperature", "\"Temperature\" BETWEEN -50 AND 60");
            table.HasCheckConstraint("CK_Telemetry_Humidity", "\"Humidity\" BETWEEN 0 AND 100");
            table.HasCheckConstraint("CK_Telemetry_Energy", "\"EnergyConsumption\" BETWEEN 0 AND 100");
        });

        // Нумерація вимірювань ведеться в межах кімнати (1, 2, 3 ...), тому ключ складений.
        builder.HasKey(t => new { t.RoomId, t.Id });
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Timestamp)
            .HasConversion(v => DateTime.SpecifyKind(v, DateTimeKind.Utc), v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        // Історія кімнати вибирається за часом, тому потрібен складений індекс.
        builder.HasIndex(t => new { t.RoomId, t.Timestamp });
    }
}
