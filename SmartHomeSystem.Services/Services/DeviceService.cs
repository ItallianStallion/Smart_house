using SmartHomeSystem.Domain.Entities;
using SmartHomeSystem.Domain.Enums;
using SmartHomeSystem.Domain.Interfaces;
using SmartHomeSystem.Services.DTOs;
using SmartHomeSystem.Services.Exceptions;
using SmartHomeSystem.Services.Interfaces;
using SmartHomeSystem.Services.Mapping;
using SmartHomeSystem.Services.Validation;

namespace SmartHomeSystem.Services.Services;

/// <summary>Пристрої належать кімнаті (агрегат SmartRoom), тому доступ до них іде через IRoomRepository.</summary>
public class DeviceService : IDeviceService
{
    private readonly IRoomRepository _rooms;

    public DeviceService(IRoomRepository rooms)
    {
        ArgumentNullException.ThrowIfNull(rooms);
        _rooms = rooms;
    }

    public async Task<DeviceDto?> GetDeviceDetailsAsync(Guid id)
    {
        var found = await FindAsync(id);
        return found is null ? null : found.Value.Device.ToDto(found.Value.Room);
    }

    public async Task<IReadOnlyList<DeviceDto>> GetAllDevicesAsync()
    {
        var rooms = await _rooms.GetAllReadOnlyAsync();
        return rooms
            .SelectMany(room => room.Devices.Select(device => device.ToDto(room)))
            .ToList();
    }

    public async Task<IReadOnlyList<DeviceDto>> SearchDevicesAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await GetAllDevicesAsync();
        }

        var normalizedQuery = query.Trim();
        var rooms = await _rooms.GetAllReadOnlyAsync();

        return rooms
            .SelectMany(room => room.Devices
                .Where(device => device.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                .Select(device => device.ToDto(room)))
            .ToList();
    }

    public async Task<DeviceDto> AddDeviceAsync(Guid roomId, CreateDeviceDto dto)
    {
        DtoValidator.Validate(dto);
        return await AddToRoomAsync(roomId, dto.Name, dto.Type);
    }

    public async Task<DeviceDto> CreateDeviceAsync(CreateDeviceRequest request)
    {
        DtoValidator.Validate(request);
        return await AddToRoomAsync(request.RoomId, request.Name, request.Type);
    }

    public async Task<DeviceDto?> UpdateDeviceAsync(Guid id, UpdateDeviceRequest request)
    {
        var found = await FindAsync(id);
        if (found is null)
        {
            return null;
        }

        var (room, device) = found.Value;
        device.Update(request.Name, request.Type); // перевірку назви/типу робить сама сутність

        await _rooms.UpdateAsync(room);
        return device.ToDto(room);
    }

    public async Task<bool> SetDeviceStateAsync(Guid id, bool isActive)
    {
        var found = await FindAsync(id);
        if (found is null)
        {
            return false;
        }

        var (room, device) = found.Value;
        if (isActive) device.Activate(); else device.Deactivate();

        await _rooms.UpdateAsync(room);
        return true;
    }

    public async Task<bool> DeleteDeviceAsync(Guid id)
    {
        var found = await FindAsync(id);
        if (found is null)
        {
            return false;
        }

        var room = found.Value.Room;
        var removed = room.RemoveDevice(id);

        await _rooms.UpdateAsync(room);
        return removed;
    }

    private async Task<DeviceDto> AddToRoomAsync(Guid roomId, string name, DeviceType type)
    {
        var room = await _rooms.GetByIdAsync(roomId) ?? throw new RoomNotFoundException(roomId);

        var device = room.AddDevice(name, type);

        await _rooms.UpdateAsync(room);
        return device.ToDto(room);
    }

    public Task<DeviceDto> ActivateDeviceAsync(Guid id) => ChangeStateAsync(id, enable: true);

    public Task<DeviceDto> DeactivateDeviceAsync(Guid id) => ChangeStateAsync(id, enable: false);

    private async Task<DeviceDto> ChangeStateAsync(Guid id, bool enable)
    {
        var (room, device) = await FindAsync(id) ?? throw new DeviceNotFoundException(id);

        if (enable) device.Activate(); else device.Deactivate();

        await _rooms.UpdateAsync(room);
        return device.ToDto(room);
    }

    private async Task<(SmartRoom Room, HomeDevice Device)?> FindAsync(Guid deviceId)
    {
        var room = await _rooms.GetByDeviceIdAsync(deviceId);
        var device = room?.FindDevice(deviceId);

        return room is null || device is null ? null : (room, device);
    }
}
