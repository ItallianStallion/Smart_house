using System.ComponentModel.DataAnnotations;
using SmartHomeSystem.Domain.Enums;

namespace SmartHomeSystem.Services.DTOs;

public class CreateDeviceDto
{
    [Required(ErrorMessage = "Назва пристрою є обов'язковою")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Назва пристрою має бути від 2 до 100 символів")]
    public string Name { get; set; } = string.Empty;

    [EnumDataType(typeof(DeviceType), ErrorMessage = "Невідомий тип пристрою")]
    public DeviceType Type { get; set; }
}
