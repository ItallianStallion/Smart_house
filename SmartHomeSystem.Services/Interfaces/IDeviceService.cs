using SmartHomeSystem.Services.DTOs;

namespace SmartHomeSystem.Services.Interfaces;

public interface IDeviceService
{
    /// <returns>DTO пристрою або null, якщо пристрою з таким Id немає.</returns>
    Task<DeviceDto?> GetDeviceDetailsAsync(Guid id);

    Task<IReadOnlyList<DeviceDto>> GetAllDevicesAsync();

    /// <summary>Пошук за назвою без урахування регістру; порожній запит повертає всі пристрої.</summary>
    Task<IReadOnlyList<DeviceDto>> SearchDevicesAsync(string query);

    Task<DeviceDto> AddDeviceAsync(Guid roomId, CreateDeviceDto dto);

    /// <summary>Лаб. 4: створює пристрій у кімнаті з request.RoomId (новий пристрій вимкнений).</summary>
    Task<DeviceDto> CreateDeviceAsync(CreateDeviceRequest request);

    /// <summary>Лаб. 4: встановлює бажаний стан (ідемпотентно).</summary>
    /// <returns>false, якщо пристрою з таким Id немає.</returns>
    /// <returns>Оновлений DTO або null, якщо пристрою з таким Id немає.</returns>
    Task<DeviceDto?> UpdateDeviceAsync(Guid id, UpdateDeviceRequest request);

    Task<bool> SetDeviceStateAsync(Guid id, bool isActive);

    /// <summary>Лаб. 4: видаляє пристрій із кімнати.</summary>
    /// <returns>false, якщо пристрою з таким Id немає.</returns>
    Task<bool> DeleteDeviceAsync(Guid id);

    Task<DeviceDto> ActivateDeviceAsync(Guid id);

    Task<DeviceDto> DeactivateDeviceAsync(Guid id);
}
