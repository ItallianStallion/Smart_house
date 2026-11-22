using System.ComponentModel.DataAnnotations;

namespace SmartHomeSystem.Services.DTOs;

public class CreateTelemetryDto
{
    [Range(-50, 60, ErrorMessage = "Температура має бути від -50 до 60 °C")]
    public double Temperature { get; set; }

    [Range(0, 100, ErrorMessage = "Вологість має бути від 0 до 100 %")]
    public double Humidity { get; set; }

    [Range(0, 100, ErrorMessage = "Споживання має бути від 0 до 100 кВт·год")]
    public double EnergyConsumption { get; set; }
}
