using SmartHomeSystem.Domain.Entities;
using SmartHomeSystem.Services.DTOs;

namespace SmartHomeSystem.Services.Mapping;

/// <summary>Єдине місце, де сутності перетворюються на DTO.</summary>
public static class DtoMapper
{
    public static RoomResponseDto ToDto(this SmartRoom room) => new()
    {
        Id = room.Id,
        Name = room.Name,
        Type = room.Type,
        Floor = room.Floor,
        Status = room.CurrentStatus,
        TargetTemperature = room.TargetTemperature,
        LastSync = room.LastSync,
        DevicesCount = room.Devices.Count,
        MeasurementsCount = room.History.Count
    };

    public static TelemetryResponseDto ToDto(this RoomTelemetry telemetry) => new()
    {
        Id = telemetry.Id,
        Temperature = telemetry.Temperature,
        Humidity = telemetry.Humidity,
        EnergyConsumption = telemetry.EnergyConsumption,
        Timestamp = telemetry.Timestamp
    };

    public static DeviceDto ToDto(this HomeDevice device, SmartRoom room) => new()
    {
        Id = device.Id,
        Name = device.Name,
        Type = device.Type,
        IsEnabled = device.IsEnabled,
        RoomId = room.Id,
        RoomName = room.Name,
        LastUpdated = device.LastUpdated
    };
}
