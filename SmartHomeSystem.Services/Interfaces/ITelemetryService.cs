using SmartHomeSystem.Services.DTOs;

namespace SmartHomeSystem.Services.Interfaces;

public interface ITelemetryService
{
    Task<IReadOnlyList<TelemetryResponseDto>> GetTelemetryAsync(Guid roomId);

    Task<TelemetryResponseDto> AddTelemetryAsync(Guid roomId, CreateTelemetryDto dto);

    Task<TelemetryResponseDto> UpdateTelemetryAsync(Guid roomId, long telemetryId, CreateTelemetryDto dto);

    Task DeleteTelemetryAsync(Guid roomId, long telemetryId);
}
