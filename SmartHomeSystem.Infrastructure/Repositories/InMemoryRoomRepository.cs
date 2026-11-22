using SmartHomeSystem.Domain.Entities;
using SmartHomeSystem.Domain.Interfaces;

namespace SmartHomeSystem.Infrastructure.Repositories;

/// <summary>Тимчасове сховище в пам'яті. Після перезапуску застосунку дані зникають.</summary>
public class InMemoryRoomRepository : IRoomRepository
{
    private readonly List<SmartRoom> _rooms = new();
    private readonly object _lock = new(); // репозиторій Singleton, а List<T> не потокобезпечний

    public Task<SmartRoom?> GetByIdAsync(Guid id)
    {
        lock (_lock)
        {
            return Task.FromResult(_rooms.FirstOrDefault(r => r.Id == id));
        }
    }

    public Task<IEnumerable<SmartRoom>> GetAllAsync()
    {
        lock (_lock)
        {
            IEnumerable<SmartRoom> rooms = _rooms.ToList();
            return Task.FromResult(rooms);
        }
    }

    public async Task<IReadOnlyList<SmartRoom>> GetAllReadOnlyAsync()
        => (await GetAllAsync()).ToList();

    public Task<SmartRoom?> GetByDeviceIdAsync(Guid deviceId)
    {
        lock (_lock)
        {
            return Task.FromResult(_rooms.FirstOrDefault(r => r.FindDevice(deviceId) is not null));
        }
    }

    public Task AddAsync(SmartRoom room)
    {
        ArgumentNullException.ThrowIfNull(room);

        lock (_lock)
        {
            if (_rooms.Any(r => r.Id == room.Id))
            {
                throw new InvalidOperationException($"Кімната із Id '{room.Id}' вже існує.");
            }

            _rooms.Add(room);
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(SmartRoom room)
    {
        ArgumentNullException.ThrowIfNull(room);

        lock (_lock)
        {
            var index = _rooms.FindIndex(r => r.Id == room.Id);
            if (index < 0)
            {
                throw new InvalidOperationException($"Кімнату із Id '{room.Id}' не знайдено.");
            }

            _rooms[index] = room;
        }

        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid id)
    {
        lock (_lock)
        {
            var removed = _rooms.RemoveAll(r => r.Id == id) > 0;
            return Task.FromResult(removed);
        }
    }
}
