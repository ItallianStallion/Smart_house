using System.Text.Json.Serialization;
using SmartHomeSystem.Domain.Enums;

namespace SmartHomeSystem.Services.DTOs;

/// <summary>Аналог DeviceDto з методички.</summary>
public class DeviceDto
{
    [JsonPropertyName("device_id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("device_type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DeviceType Type { get; set; }

    [JsonPropertyName("is_enabled")]
    public bool IsEnabled { get; set; }

    [JsonPropertyName("room_id")]
    public Guid RoomId { get; set; }

    [JsonPropertyName("room_name")]
    public string RoomName { get; set; } = string.Empty;

    [JsonPropertyName("last_updated")]
    public DateTime LastUpdated { get; set; }
}
