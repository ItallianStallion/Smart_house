namespace SmartHomeSystem.Services.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

public class RoomNotFoundException : NotFoundException
{
    public RoomNotFoundException(Guid id) : base($"Кімнату з Id '{id}' не знайдено.") { }
}

public class TelemetryNotFoundException : NotFoundException
{
    public TelemetryNotFoundException(long id) : base($"Вимірювання з Id '{id}' не знайдено.") { }
}
