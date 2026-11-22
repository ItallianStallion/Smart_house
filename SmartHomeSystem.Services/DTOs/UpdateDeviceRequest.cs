using SmartHomeSystem.Domain.Enums;

namespace SmartHomeSystem.Services.DTOs;

/// <summary>Модель запиту PUT /api/devices/{id}: повний набір редагованих полів (назва й тип).</summary>
public sealed class UpdateDeviceRequest
{
    public string Name { get; init; } = string.Empty;

    public DeviceType Type { get; init; }
}
