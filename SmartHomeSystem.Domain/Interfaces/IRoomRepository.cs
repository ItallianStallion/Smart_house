using SmartHomeSystem.Domain.Entities;

namespace SmartHomeSystem.Domain.Interfaces;

public interface IRoomRepository
{
    Task<SmartRoom?> GetByIdAsync(Guid id);

    /// <summary>Усі кімнати для подальших змін (сутності відстежуються контекстом).</summary>
    Task<IEnumerable<SmartRoom>> GetAllAsync();

    /// <summary>Усі кімнати лише для читання (без відстеження змін): швидше й дешевше, коли дані лише перетворюються на DTO.</summary>
    Task<IReadOnlyList<SmartRoom>> GetAllReadOnlyAsync();

    /// <summary>Кімната, у якій розташований пристрій, або null. Одним запитом, без завантаження всіх кімнат.</summary>
    Task<SmartRoom?> GetByDeviceIdAsync(Guid deviceId);

    Task AddAsync(SmartRoom room);

    Task UpdateAsync(SmartRoom room);

    /// <returns>true, якщо кімнату було видалено.</returns>
    Task<bool> DeleteAsync(Guid id);
}
