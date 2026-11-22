using SmartHomeSystem.Domain.Exceptions;

namespace SmartHomeSystem.Domain.Entities;

public class SmartHome
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;

    private SmartHome() { } // для EF Core

    public SmartHome(string name, string address)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Назва будинку не може бути порожньою.");
        }

        Name = name.Trim();
        Address = address?.Trim() ?? string.Empty;
    }
}
