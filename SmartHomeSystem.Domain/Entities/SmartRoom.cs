using SmartHomeSystem.Domain.Enums;
using SmartHomeSystem.Domain.Exceptions;

namespace SmartHomeSystem.Domain.Entities;

/// <summary>
/// Головна доменна сутність. Сама керує своїм станом: зовні змінити статус
/// можна лише через методи (AddTelemetry, RaiseAlarm ...), а не присвоєнням.
/// </summary>
public class SmartRoom
{
    private const double WarningHumidity = 70;
    private const double WarningTemperatureDelta = 5;

    private readonly List<HomeDevice> _devices = new();
    private readonly List<RoomTelemetry> _history = new();

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public RoomType Type { get; private set; }
    public int Floor { get; private set; }
    public int TargetTemperature { get; private set; }               // бажана температура, °C
    public RoomStatus CurrentStatus { get; private set; } = RoomStatus.Vacant;
    public DateTime LastSync { get; private set; }
    public Guid HomeId { get; private set; }                         // зв'язок із SmartHome

    public IReadOnlyList<HomeDevice> Devices => _devices;
    public IReadOnlyList<RoomTelemetry> History => _history;

    private SmartRoom() { } // для EF Core

    public SmartRoom(string name, RoomType type, int floor, int targetTemperature, Guid homeId)
    {
        ApplyDetails(name, type, floor, targetTemperature);
        HomeId = homeId;
    }

    /// <summary>Змінює параметри кімнати; бажана температура впливає на статус, тому він перераховується.</summary>
    public void Update(string name, RoomType type, int floor, int targetTemperature)
    {
        ApplyDetails(name, type, floor, targetTemperature);
        RecalculateState();
    }

    public HomeDevice AddDevice(string name, DeviceType type)
    {
        var device = new HomeDevice(name, type, Id);
        _devices.Add(device);
        return device;
    }

    public HomeDevice? FindDevice(Guid deviceId) => _devices.FirstOrDefault(d => d.Id == deviceId);

    /// <returns>true, якщо пристрій було видалено з кімнати.</returns>
    public bool RemoveDevice(Guid deviceId)
    {
        var device = FindDevice(deviceId);
        return device is not null && _devices.Remove(device);
    }

    /// <summary>Останнє вимірювання (за часом, а за рівних часів — за Id).</summary>
    public RoomTelemetry? LastTelemetry =>
        _history.OrderByDescending(h => h.Timestamp).ThenByDescending(h => h.Id).FirstOrDefault();

    /// <summary>Додає вимірювання, оновлює час синхронізації та статус.</summary>
    public RoomTelemetry AddTelemetry(double temperature, double humidity,
        double energyConsumption, DateTime? timestamp = null)
    {
        var record = new RoomTelemetry(
            id: _history.Count == 0 ? 1 : _history.Max(h => h.Id) + 1, // не повторюється після видалень
            temperature, humidity, energyConsumption,
            timestamp ?? DateTime.UtcNow,
            Id);

        _history.Add(record);
        RecalculateState();
        return record;
    }

    /// <returns>Оновлене вимірювання або null, якщо його немає в цій кімнаті.</returns>
    public RoomTelemetry? UpdateTelemetry(long telemetryId, double temperature,
        double humidity, double energyConsumption)
    {
        var record = _history.FirstOrDefault(t => t.Id == telemetryId);
        if (record is null)
        {
            return null;
        }

        record.Update(temperature, humidity, energyConsumption);
        RecalculateState();
        return record;
    }

    /// <returns>true, якщо вимірювання було видалено.</returns>
    public bool RemoveTelemetry(long telemetryId)
    {
        var record = _history.FirstOrDefault(t => t.Id == telemetryId);
        if (record is null)
        {
            return false;
        }

        _history.Remove(record);
        RecalculateState();
        return true;
    }

    public void RaiseAlarm() => CurrentStatus = RoomStatus.Alarm;

    public void ClearAlarm()
    {
        CurrentStatus = RoomStatus.Vacant;
        RecalculateState();
    }

    // Статус і час синхронізації завжди виводяться з історії вимірювань.
    // Тривогу ставить окрема логіка, тому її тут не перезаписуємо.
    private void RecalculateState()
    {
        // ThenBy Id: при однакових Timestamp (низька роздільність годинника) останнім вважається пізніше додане.
        var last = _history.OrderByDescending(h => h.Timestamp).ThenByDescending(h => h.Id).FirstOrDefault();
        LastSync = last?.Timestamp ?? default;

        if (CurrentStatus == RoomStatus.Alarm)
        {
            return;
        }

        if (last is null)
        {
            CurrentStatus = RoomStatus.Vacant;
            return;
        }

        CurrentStatus = (last.Temperature > TargetTemperature + WarningTemperatureDelta
                         || last.Humidity > WarningHumidity)
            ? RoomStatus.Warning
            : RoomStatus.Occupied;
    }

    private void ApplyDetails(string name, RoomType type, int floor, int targetTemperature)
    {
        var trimmed = name?.Trim() ?? string.Empty;

        if (trimmed.Length < 3 || trimmed.Length > 100)
            throw new DomainValidationException("Назва має бути від 3 до 100 символів");

        if (!Enum.IsDefined(type))
            throw new DomainValidationException("Невідомий тип кімнати");

        if (floor < 0 || floor > 10)
            throw new DomainValidationException("Поверх має бути від 0 до 10");

        if (targetTemperature < 10 || targetTemperature > 30)
            throw new DomainValidationException("Бажана температура має бути від 10 до 30 °C");

        Name = trimmed;
        Type = type;
        Floor = floor;
        TargetTemperature = targetTemperature;
    }
}
