namespace SmartHomeSystem.Domain.Exceptions;

/// <summary>Порушення інваріантів доменної моделі (некоректні значення полів сутності).</summary>
public class DomainValidationException : Exception
{
    public DomainValidationException(string message) : base(message) { }
}
