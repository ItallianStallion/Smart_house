using System.Net;
using SmartHomeSystem.Services.DTOs;
using SmartHomeSystem.Services.Interfaces;

namespace HomeConsole.Api;

// Ті самі інтерфейси сервісного шару (IRoomService, IDeviceService, ITelemetryService),
// але реалізовані через HTTP. Консоль не знає, де працює логіка — це і є Dependency Injection.

public sealed class ApiRoomService : IRoomService
{
    private readonly ApiClient _api;
    public ApiRoomService(ApiClient api) => _api = api;

    public async Task<IReadOnlyList<RoomResponseDto>> GetRoomsAsync()
        => await _api.SendAsync<List<RoomResponseDto>>(HttpMethod.Get, "api/rooms");

    public Task<RoomResponseDto?> GetRoomDetailsAsync(Guid id)
        => _api.GetOrNullAsync<RoomResponseDto>($"api/rooms/{id}");

    public async Task<IReadOnlyList<RoomResponseDto>> SearchRoomsAsync(string query)
        => await _api.SendAsync<List<RoomResponseDto>>(
            HttpMethod.Get, $"api/rooms/search?query={Uri.EscapeDataString(query ?? string.Empty)}");

    public Task<RoomResponseDto> CreateRoomAsync(CreateRoomDto dto)
        => _api.SendAsync<RoomResponseDto>(HttpMethod.Post, "api/rooms", dto);

    public Task<RoomResponseDto> UpdateRoomAsync(Guid id, CreateRoomDto dto)
        => _api.SendAsync<RoomResponseDto>(HttpMethod.Put, $"api/rooms/{id}", dto);

    public Task DeleteRoomAsync(Guid id)
        => _api.SendAsync(HttpMethod.Delete, $"api/rooms/{id}");
}

public sealed class ApiDeviceService : IDeviceService
{
    private readonly ApiClient _api;
    public ApiDeviceService(ApiClient api) => _api = api;

    public Task<DeviceDto?> GetDeviceDetailsAsync(Guid id)
        => _api.GetOrNullAsync<DeviceDto>($"api/devices/{id}");

    public async Task<IReadOnlyList<DeviceDto>> GetAllDevicesAsync()
        => await _api.SendAsync<List<DeviceDto>>(HttpMethod.Get, "api/devices");

    public async Task<IReadOnlyList<DeviceDto>> SearchDevicesAsync(string query)
        => await _api.SendAsync<List<DeviceDto>>(
            HttpMethod.Get, $"api/devices/search?query={Uri.EscapeDataString(query ?? string.Empty)}");

    public Task<DeviceDto> AddDeviceAsync(Guid roomId, CreateDeviceDto dto)
        => _api.SendAsync<DeviceDto>(HttpMethod.Post, $"api/rooms/{roomId}/devices", dto);

    public Task<DeviceDto> CreateDeviceAsync(CreateDeviceRequest request)
        => _api.SendAsync<DeviceDto>(HttpMethod.Post, "api/devices", request);

    public async Task<DeviceDto?> UpdateDeviceAsync(Guid id, UpdateDeviceRequest request)
    {
        try
        {
            return await _api.SendAsync<DeviceDto>(HttpMethod.Put, $"api/devices/{id}", request);
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> SetDeviceStateAsync(Guid id, bool isActive)
    {
        try
        {
            await _api.SendAsync(HttpMethod.Patch, $"api/devices/{id}/status", new SetDeviceStatusRequest { IsActive = isActive });
            return true;
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<bool> DeleteDeviceAsync(Guid id)
    {
        try
        {
            await _api.SendAsync(HttpMethod.Delete, $"api/devices/{id}");
            return true;
        }
        catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public Task<DeviceDto> ActivateDeviceAsync(Guid id)
        => _api.SendAsync<DeviceDto>(HttpMethod.Post, $"api/devices/{id}/activate");

    public Task<DeviceDto> DeactivateDeviceAsync(Guid id)
        => _api.SendAsync<DeviceDto>(HttpMethod.Post, $"api/devices/{id}/deactivate");
}

public sealed class ApiTelemetryService : ITelemetryService
{
    private readonly ApiClient _api;
    public ApiTelemetryService(ApiClient api) => _api = api;

    public async Task<IReadOnlyList<TelemetryResponseDto>> GetTelemetryAsync(Guid roomId)
        => await _api.SendAsync<List<TelemetryResponseDto>>(HttpMethod.Get, $"api/rooms/{roomId}/telemetry");

    public Task<TelemetryResponseDto> AddTelemetryAsync(Guid roomId, CreateTelemetryDto dto)
        => _api.SendAsync<TelemetryResponseDto>(HttpMethod.Post, $"api/rooms/{roomId}/telemetry", dto);

    public Task<TelemetryResponseDto> UpdateTelemetryAsync(Guid roomId, long telemetryId, CreateTelemetryDto dto)
        => _api.SendAsync<TelemetryResponseDto>(HttpMethod.Put, $"api/rooms/{roomId}/telemetry/{telemetryId}", dto);

    public Task DeleteTelemetryAsync(Guid roomId, long telemetryId)
        => _api.SendAsync(HttpMethod.Delete, $"api/rooms/{roomId}/telemetry/{telemetryId}");
}
