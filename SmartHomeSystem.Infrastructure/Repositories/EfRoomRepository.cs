using Microsoft.EntityFrameworkCore;
using SmartHomeSystem.Domain.Entities;
using SmartHomeSystem.Domain.Interfaces;
using SmartHomeSystem.Infrastructure.Data;

namespace SmartHomeSystem.Infrastructure.Repositories;

/// <summary>
/// Постійний репозиторій кімнат (EF Core). Реалізує той самий IRoomRepository, що й InMemoryRoomRepository,
/// тому сервіси, контролери й DTO про базу даних нічого не знають.
/// Час життя — Scoped: один DbContext на один HTTP-запит.
/// </summary>
public class EfRoomRepository : IRoomRepository
{
    private readonly SmartHomeDbContext _db;

    public EfRoomRepository(SmartHomeDbContext db) => _db = db;

    // Кімната — агрегат: завантажуємо її разом із пристроями та історією вимірювань.
    private IQueryable<SmartRoom> RoomsWithChildren()
        => _db.Rooms.Include(r => r.Devices).Include(r => r.History).AsSplitQuery();

    public Task<SmartRoom?> GetByIdAsync(Guid id)
        => RoomsWithChildren().FirstOrDefaultAsync(r => r.Id == id);

    public async Task<IEnumerable<SmartRoom>> GetAllAsync()
        => await RoomsWithChildren().ToListAsync();

    // Лише читання: AsNoTracking не створює знімків для відстеження змін, тому запит швидший і споживає менше пам'яті.
    public async Task<IReadOnlyList<SmartRoom>> GetAllReadOnlyAsync()
        => await RoomsWithChildren().AsNoTracking().ToListAsync();

    // Один запит із фільтром за пристроєм замість завантаження всіх кімнат з дочірніми даними.
    public Task<SmartRoom?> GetByDeviceIdAsync(Guid deviceId)
        => RoomsWithChildren().FirstOrDefaultAsync(r => r.Devices.Any(d => d.Id == deviceId));

    public async Task AddAsync(SmartRoom room)
    {
        ArgumentNullException.ThrowIfNull(room);

        if (await _db.Rooms.AnyAsync(r => r.Id == room.Id))
        {
            throw new InvalidOperationException($"Кімната із Id '{room.Id}' вже існує.");
        }

        _db.Rooms.Add(room);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(SmartRoom room)
    {
        ArgumentNullException.ThrowIfNull(room);

        // Кімнату завантажено в цьому ж запиті, тому EF уже відстежує зміни (у тому числі додані/видалені дочірні сутності).
        if (_db.Entry(room).State == EntityState.Detached)
        {
            throw new InvalidOperationException($"Кімнату із Id '{room.Id}' не знайдено.");
        }

        await _db.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var room = await _db.Rooms.FirstOrDefaultAsync(r => r.Id == id);
        if (room is null)
        {
            return false;
        }

        _db.Rooms.Remove(room); // пристрої та вимірювання видаляються каскадом
        await _db.SaveChangesAsync();
        return true;
    }
}
