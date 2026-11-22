using SmartHomeSystem.Services.DTOs;

namespace SmartHomeSystem.Services.Interfaces;

public interface IRoomService
{
    Task<IReadOnlyList<RoomResponseDto>> GetRoomsAsync();

    /// <returns>DTO кімнати або null, якщо кімнати з таким Id немає.</returns>
    Task<RoomResponseDto?> GetRoomDetailsAsync(Guid id);

    /// <summary>Пошук за назвою без урахування регістру; порожній запит повертає всі кімнати.</summary>
    Task<IReadOnlyList<RoomResponseDto>> SearchRoomsAsync(string query);

    Task<RoomResponseDto> CreateRoomAsync(CreateRoomDto dto);

    Task<RoomResponseDto> UpdateRoomAsync(Guid id, CreateRoomDto dto);

    Task DeleteRoomAsync(Guid id);
}
