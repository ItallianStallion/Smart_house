using SmartHomeSystem.Domain.Enums;
using SmartHomeSystem.Domain.Exceptions;

namespace SmartHomeSystem.Domain.Entities;

/// <summary>Аналог GreenhouseDevice з методички: новий пристрій вимкнений, станом керують Activate/Deactivate.</summary>
public class HomeDevice
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public DeviceType Type { get; private set; }
    public bool IsEnabled { get; private set; }
    public Guid RoomId { get; private set; }

    /// <summary>Час останньої зміни пристрою (UTC): створення, оновлення, увімкнення/вимкнення.</summary>
    public DateTime LastUpdated { get; private set; } = DateTime.UtcNow;

    // Потрібен лише EF Core для матеріалізації сутності з бази; бізнес-код його не викликає.
    private HomeDevice() { }

    internal HomeDevice(string name, DeviceType type, Guid roomId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Назва пристрою не може бути порожньою.");
        }

        Name = name.Trim();
        Type = type;
        RoomId = roomId;
        IsEnabled = false;
        LastUpdated = DateTime.UtcNow;
    }

    /// <summary>Повне оновлення редагованих полів (PUT /api/devices/{id}).</summary>
    public void Update(string name, DeviceType type)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Назва пристрою не може бути порожньою.");
        }

        if (!Enum.IsDefined(type))
        {
            throw new DomainValidationException("Невідомий тип пристрою");
        }

        Name = name.Trim();
        Type = type;
        LastUpdated = DateTime.UtcNow;
    }

    public void Activate() => SetEnabled(true);

    public void Deactivate() => SetEnabled(false);

    // Час змінюється лише тоді, коли стан справді змінився (повторний запит нічого не змінює).
    private void SetEnabled(bool enabled)
    {
        if (IsEnabled == enabled)
        {
            return;
        }

        IsEnabled = enabled;
        LastUpdated = DateTime.UtcNow;
    }
}
