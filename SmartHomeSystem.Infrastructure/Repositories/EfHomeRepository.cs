using Microsoft.EntityFrameworkCore;
using SmartHomeSystem.Domain.Entities;
using SmartHomeSystem.Domain.Interfaces;
using SmartHomeSystem.Infrastructure.Data;

namespace SmartHomeSystem.Infrastructure.Repositories;

public class EfHomeRepository : IHomeRepository
{
    private readonly SmartHomeDbContext _db;

    public EfHomeRepository(SmartHomeDbContext db) => _db = db;

    /// <summary>Повертає єдиний будинок; якщо база порожня — створює його при першому зверненні.</summary>
    public async Task<SmartHome> GetCurrentAsync()
    {
        var home = await _db.Homes.OrderBy(h => h.Name).FirstOrDefaultAsync();
        if (home is not null)
        {
            return home;
        }

        home = new SmartHome("Мій розумний будинок", "вул. Прикладна, 1");
        _db.Homes.Add(home);
        await _db.SaveChangesAsync();
        return home;
    }
}
