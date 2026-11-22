using SmartHomeSystem.Domain.Entities;
using SmartHomeSystem.Domain.Interfaces;

namespace SmartHomeSystem.Infrastructure.Repositories;

public class InMemoryHomeRepository : IHomeRepository
{
    private readonly SmartHome _home = new("Мій розумний будинок", "вул. Прикладна, 1");

    public Task<SmartHome> GetCurrentAsync() => Task.FromResult(_home);
}
