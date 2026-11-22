using SmartHomeSystem.Domain.Entities;
using SmartHomeSystem.Domain.Interfaces;
using SmartHomeSystem.Services.DTOs;
using SmartHomeSystem.Services.Exceptions;
using SmartHomeSystem.Services.Interfaces;
using SmartHomeSystem.Services.Mapping;
using SmartHomeSystem.Services.Validation;

namespace SmartHomeSystem.Services.Services;

public class RoomService : IRoomService
{
    private readonly IRoomRepository _rooms;
    private readonly IHomeRepository _homes;

    public RoomService(IRoomRepository rooms, IHomeRepository homes)
    {
        ArgumentNullException.ThrowIfNull(rooms);
        ArgumentNullException.ThrowIfNull(homes);
        _rooms = rooms;
        _homes = homes;
    }

    public async Task<IReadOnlyList<RoomResponseDto>> GetRoomsAsync()
    {
        var rooms = await _rooms.GetAllReadOnlyAsync();
        return rooms.Select(r => r.ToDto()).ToList();
    }

    public async Task<RoomResponseDto?> GetRoomDetailsAsync(Guid id)
    {
        var room = await _rooms.GetByIdAsync(id);
        return room?.ToDto();
    }

    public async Task<IReadOnlyList<RoomResponseDto>> SearchRoomsAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await GetRoomsAsync();
        }

        var normalizedQuery = query.Trim();
        var rooms = await _rooms.GetAllReadOnlyAsync();

        return rooms
            .Where(room => room.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .Select(room => room.ToDto())
            .ToList();
    }

    public async Task<RoomResponseDto> CreateRoomAsync(CreateRoomDto dto)
    {
        DtoValidator.Validate(dto);
        await EnsureNameIsUniqueAsync(dto.Name, exceptRoomId: null);

        var home = await _homes.GetCurrentAsync();
        var room = new SmartRoom(dto.Name, dto.Type, dto.Floor, dto.TargetTemperature, home.Id);

        await _rooms.AddAsync(room);
        return room.ToDto();
    }

    public async Task<RoomResponseDto> UpdateRoomAsync(Guid id, CreateRoomDto dto)
    {
        DtoValidator.Validate(dto);

        var room = await _rooms.GetByIdAsync(id) ?? throw new RoomNotFoundException(id);
        await EnsureNameIsUniqueAsync(dto.Name, exceptRoomId: id);

        room.Update(dto.Name, dto.Type, dto.Floor, dto.TargetTemperature);

        await _rooms.UpdateAsync(room);
        return room.ToDto();
    }

    public async Task DeleteRoomAsync(Guid id)
    {
        if (!await _rooms.DeleteAsync(id))
        {
            throw new RoomNotFoundException(id);
        }
    }

    // Бізнес-правило: назви кімнат в одному будинку не повторюються (без урахування регістру).
    private async Task EnsureNameIsUniqueAsync(string name, Guid? exceptRoomId)
    {
        var trimmed = name.Trim();
        var rooms = await _rooms.GetAllReadOnlyAsync();

        var exists = rooms.Any(r =>
            r.Id != exceptRoomId &&
            string.Equals(r.Name, trimmed, StringComparison.OrdinalIgnoreCase));

        if (exists)
        {
            throw new DuplicateRoomNameException(trimmed);
        }
    }
}
