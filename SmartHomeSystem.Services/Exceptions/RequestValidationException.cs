namespace SmartHomeSystem.Services.Exceptions;

public class RequestValidationException : Exception
{
    public RequestValidationException(string message) : base(message) { }
}
