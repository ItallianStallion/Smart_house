using SmartHomeSystem.Domain.Enums;

namespace SmartHomeSystem.Services.DTOs;

/// <summary>
/// Модель запиту POST /api/devices. Пристрій у «Розумному будинку» завжди належить кімнаті.
/// Правила перевірки винесено в CreateDeviceRequestValidator (FluentValidation, проєкт API).
/// </summary>
public sealed class CreateDeviceRequest
{
    /// <summary>Назва пристрою (2–100 символів, не може бути порожньою).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Тип пристрою: Light, Thermostat, DoorLock, MotionSensor, SmokeDetector.</summary>
    public DeviceType Type { get; init; }

    /// <summary>Ідентифікатор кімнати, у якій встановлено пристрій.</summary>
    public Guid RoomId { get; init; }
}
