using SmartHomeSystem.Domain.Enums;
using SmartHomeSystem.Domain.Interfaces;
using SmartHomeSystem.Services.Exceptions;
using SmartHomeSystem.Services.Interfaces;

namespace SmartHomeSystem.Services.Services;

/// <summary>
/// Бізнес-правила керування будинком (методичка: «якщо вологість &lt; 30% — увімкнути полив»).
/// Тут: термостат реагує на температуру, критична температура вмикає тривогу.
/// </summary>
public class ClimateControlService : IClimateControlService
{
    private const double HeatingMargin = 1;        // вмикаємо опалення, коли холодніше за ціль більш ніж на 1 °C
    private const double FireTemperature = 50;     // °C — підозра на пожежу

    private readonly IRoomRepository _rooms;

    public ClimateControlService(IRoomRepository rooms)
    {
        ArgumentNullException.ThrowIfNull(rooms);
        _rooms = rooms;
    }

    public async Task<IReadOnlyList<string>> ApplyRulesAsync(Guid roomId)
    {
        var room = await _rooms.GetByIdAsync(roomId) ?? throw new RoomNotFoundException(roomId);
        var actions = new List<string>();

        var last = room.LastTelemetry;
        if (last is null)
        {
            return actions;
        }

        // Правило 1: термостат.
        foreach (var thermostat in room.Devices.Where(d => d.Type == DeviceType.Thermostat))
        {
            if (last.Temperature < room.TargetTemperature - HeatingMargin && !thermostat.IsEnabled)
            {
                thermostat.Activate();
                actions.Add($"{last.Temperature:0.#} °C < цілі {room.TargetTemperature} °C -> «{thermostat.Name}» УВІМКНЕНО");
            }
            else if (last.Temperature >= room.TargetTemperature && thermostat.IsEnabled)
            {
                thermostat.Deactivate();
                actions.Add($"{last.Temperature:0.#} °C >= цілі {room.TargetTemperature} °C -> «{thermostat.Name}» ВИМКНЕНО");
            }
        }

        // Правило 2: критична температура -> тривога.
        if (last.Temperature >= FireTemperature && room.CurrentStatus != RoomStatus.Alarm)
        {
            room.RaiseAlarm();
            actions.Add($"{last.Temperature:0.#} °C >= {FireTemperature} °C -> ТРИВОГА в «{room.Name}»");
        }

        if (actions.Count > 0)
        {
            await _rooms.UpdateAsync(room);
        }

        return actions;
    }
}
