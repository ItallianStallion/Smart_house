namespace SmartHomeSystem.Services.Exceptions;

public class DeviceNotFoundException : NotFoundException
{
    public DeviceNotFoundException(Guid id) : base($"Пристрій з Id '{id}' не знайдено.") { }
}
