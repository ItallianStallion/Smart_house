using System.Text.Json.Serialization;
using SmartHomeSystem.Domain.Enums;

namespace SmartHomeSystem.Services.DTOs;

// Імена JSON-полів збігаються з тим, що очікує клієнт Lab2 (HttpHomeService).
public class RoomResponseDto
{
    [JsonPropertyName("room_id")]
    public Guid Id { get; set; }

    [JsonPropertyName("display_name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("room_type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RoomType Type { get; set; }

    [JsonPropertyName("floor")]
    public int Floor { get; set; }

    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RoomStatus Status { get; set; }

    [JsonPropertyName("target_temperature")]
    public int TargetTemperature { get; set; }

    [JsonPropertyName("last_sync")]
    public DateTime LastSync { get; set; }

    [JsonPropertyName("devices_count")]
    public int DevicesCount { get; set; }

    [JsonPropertyName("measurements_count")]
    public int MeasurementsCount { get; set; }
}
