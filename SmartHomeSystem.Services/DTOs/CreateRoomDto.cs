using System.ComponentModel.DataAnnotations;
using SmartHomeSystem.Domain.Enums;

namespace SmartHomeSystem.Services.DTOs;

public class CreateRoomDto
{
    [Required(ErrorMessage = "Назва кімнати є обов'язковою")]
    [StringLength(100, MinimumLength = 3,
        ErrorMessage = "Назва має бути від 3 до 100 символів")]
    public string Name { get; set; } = string.Empty;

    [EnumDataType(typeof(RoomType), ErrorMessage = "Невідомий тип кімнати")]
    public RoomType Type { get; set; }

    [Range(0, 10, ErrorMessage = "Поверх має бути від 0 до 10")]
    public int Floor { get; set; }

    [Range(10, 30, ErrorMessage = "Бажана температура має бути від 10 до 30 °C")]
    public int TargetTemperature { get; set; }
}
