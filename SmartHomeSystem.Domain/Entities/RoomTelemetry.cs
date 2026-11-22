using SmartHomeSystem.Domain.Exceptions;

namespace SmartHomeSystem.Domain.Entities;

public class RoomTelemetry
{
    public long Id { get; private set; }
    public double Temperature { get; private set; }          // °C
    public double Humidity { get; private set; }             // %
    public double EnergyConsumption { get; private set; }    // кВт·год
    public DateTime Timestamp { get; private set; }
    public Guid RoomId { get; private set; }

    private RoomTelemetry() { } // для EF Core

    // Створюється лише всередині SmartRoom (він відповідає за нумерацію та стан).
    internal RoomTelemetry(long id, double temperature, double humidity,
        double energyConsumption, DateTime timestamp, Guid roomId)
    {
        EnsureValid(temperature, humidity, energyConsumption);

        Id = id;
        Temperature = temperature;
        Humidity = humidity;
        EnergyConsumption = energyConsumption;
        Timestamp = timestamp;
        RoomId = roomId;
    }

    internal void Update(double temperature, double humidity, double energyConsumption)
    {
        EnsureValid(temperature, humidity, energyConsumption);

        Temperature = temperature;
        Humidity = humidity;
        EnergyConsumption = energyConsumption;
    }

    private static void EnsureValid(double temperature, double humidity, double energy)
    {
        if (!(temperature >= -50 && temperature <= 60))
            throw new DomainValidationException("Температура має бути від -50 до 60 °C");

        if (!(humidity >= 0 && humidity <= 100))
            throw new DomainValidationException("Вологість має бути від 0 до 100 %");

        if (!(energy >= 0 && energy <= 100))
            throw new DomainValidationException("Споживання має бути від 0 до 100 кВт·год");
    }
}
