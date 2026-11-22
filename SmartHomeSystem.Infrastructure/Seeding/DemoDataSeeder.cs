using SmartHomeSystem.Domain.Entities;
using SmartHomeSystem.Domain.Enums;
using SmartHomeSystem.Domain.Interfaces;

namespace SmartHomeSystem.Infrastructure.Seeding;

/// <summary>Тестові дані (ті самі, що в MockHomeService із Lab2).</summary>
public static class DemoDataSeeder
{
    public static async Task SeedAsync(IHomeRepository homes, IRoomRepository rooms)
    {
        var home = await homes.GetCurrentAsync();

        var living = new SmartRoom("Вітальня", RoomType.LivingRoom, 1, 22, home.Id);
        living.AddDevice("Термостат", DeviceType.Thermostat);
        living.AddDevice("Світло у вітальні", DeviceType.Light);
        living.AddDevice("Датчик руху", DeviceType.MotionSensor).Activate();
        Reading(living, 21.8, 48.0, 0.45, 45);
        Reading(living, 22.3, 50.5, 0.52, 30);
        Reading(living, 23.0, 52.1, 0.48, 15);

        var kitchen = new SmartRoom("Кухня", RoomType.Kitchen, 1, 21, home.Id);
        kitchen.AddDevice("Датчик диму", DeviceType.SmokeDetector).Activate();
        kitchen.AddDevice("Термостат кухні", DeviceType.Thermostat);
        Reading(kitchen, 24.5, 60.0, 0.90, 20);
        Reading(kitchen, 27.5, 78.0, 1.90, 5);          // -> статус Warning

        var bedroom = new SmartRoom("Спальня", RoomType.Bedroom, 2, 19, home.Id); // без вимірювань
        bedroom.AddDevice("Термостат спальні", DeviceType.Thermostat);
        bedroom.AddDevice("Розумний замок", DeviceType.DoorLock).Activate();

        var hall = new SmartRoom("Передпокій", RoomType.Hallway, 1, 20, home.Id);
        Reading(hall, 19.0, 45.0, 0.10, 10);
        hall.RaiseAlarm();                               // тестовий стан «Тривога»

        foreach (var room in new[] { living, kitchen, bedroom, hall })
        {
            await rooms.AddAsync(room);
        }
    }

    private static void Reading(SmartRoom room, double t, double h, double e, int minutesAgo)
        => room.AddTelemetry(t, h, e, DateTime.UtcNow.AddMinutes(-minutesAgo));
}
