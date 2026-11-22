namespace SmartHomeSystem.Services.DTOs;

/// <summary>Модель запиту PATCH /api/devices/{id}/status: передається бажаний стан, а не «перемкнути».</summary>
public sealed class SetDeviceStatusRequest
{
    /// <summary>true — увімкнути пристрій, false — вимкнути.</summary>
    /// <remarks>bool?: якщо поле не передано, SetDeviceStatusRequestValidator поверне 400, а не мовчки вимкне пристрій.</remarks>
    public bool? IsActive { get; init; }
}
