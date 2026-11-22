namespace SmartHomeSystem.Services.Exceptions;

public class DuplicateRoomNameException : Exception
{
    public DuplicateRoomNameException(string name)
        : base($"Кімната з назвою '{name}' вже існує.") { }
}
