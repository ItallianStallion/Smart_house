using SmartHomeSystem.Domain.Entities;
using SmartHomeSystem.Domain.Interfaces;
using SmartHomeSystem.Services.DTOs;
using SmartHomeSystem.Services.Exceptions;
using SmartHomeSystem.Services.Interfaces;
using SmartHomeSystem.Services.Mapping;
using SmartHomeSystem.Services.Validation;

namespace SmartHomeSystem.Services.Services;

public class TelemetryService : ITelemetryService
{
    private readonly IRoomRepository _rooms;
    private readonly IClimateControlService _climate;

    public TelemetryService(IRoomRepository rooms, IClimateControlService climate)
    {
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(climate);
        _rooms = rooms;
        _climate = climate;
    }

    public async Task<IReadOnlyList<TelemetryResponseDto>> GetTelemetryAsync(Guid roomId)
    {
        var room = await GetRoomAsync(roomId);
        return room.History.Select(t => t.ToDto()).ToList();
    }

    public async Task<TelemetryResponseDto> AddTelemetryAsync(Guid roomId, CreateTelemetryDto dto)
    {
        DtoValidator.Validate(dto);
        var room = await GetRoomAsync(roomId);

        var record = room.AddTelemetry(dto.Temperature, dto.Humidity, dto.EnergyConsumption);

        await _rooms.UpdateAsync(room);
        await _climate.ApplyRulesAsync(roomId); // автоматизація реагує на нові дані
        return record.ToDto();
    }

    public async Task<TelemetryResponseDto> UpdateTelemetryAsync(
        Guid roomId, long telemetryId, CreateTelemetryDto dto)
    {
        DtoValidator.Validate(dto);
        var room = await GetRoomAsync(roomId);

        var record = room.UpdateTelemetry(telemetryId, dto.Temperature, dto.Humidity, dto.EnergyConsumption)
                     ?? throw new TelemetryNotFoundException(telemetryId);

        await _rooms.UpdateAsync(room);
        return record.ToDto();
    }

    public async Task DeleteTelemetryAsync(Guid roomId, long telemetryId)
    {
        var room = await GetRoomAsync(roomId);

        if (!room.RemoveTelemetry(telemetryId))
        {
            throw new TelemetryNotFoundException(telemetryId);
        }

        await _rooms.UpdateAsync(room);
    }

    private async Task<SmartRoom> GetRoomAsync(Guid roomId)
        => await _rooms.GetByIdAsync(roomId) ?? throw new RoomNotFoundException(roomId);
}
