using System.Text;
using HomeConsole.Api;
using Microsoft.Extensions.DependencyInjection;
using SmartHomeSystem.Domain.Enums;
using SmartHomeSystem.Domain.Exceptions;
using SmartHomeSystem.Services.DTOs;
using SmartHomeSystem.Services.Exceptions;
using SmartHomeSystem.Services.Interfaces;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

// Адреса API: аргумент командного рядка, змінна SMARTHOME_API або http://localhost:5000
var apiUrl = args.Length > 0
    ? args[0]
    : Environment.GetEnvironmentVariable("SMARTHOME_API") ?? "http://localhost:5000";

// ===== Dependency Injection =====
// Консоль має ті самі інтерфейси сервісів, що й API, але реалізовані через HTTP-запити до API.
var services = new ServiceCollection();
services.AddSingleton(new ApiClient(new HttpClient { BaseAddress = new Uri(apiUrl.TrimEnd('/') + "/") }));
services.AddScoped<IRoomService, ApiRoomService>();
services.AddScoped<IDeviceService, ApiDeviceService>();
services.AddScoped<ITelemetryService, ApiTelemetryService>();

await using var provider = services.BuildServiceProvider(validateScopes: true);
using var scope = provider.CreateScope();
var rooms = scope.ServiceProvider.GetRequiredService<IRoomService>();
var devices = scope.ServiceProvider.GetRequiredService<IDeviceService>();
var telemetry = scope.ServiceProvider.GetRequiredService<ITelemetryService>();

try
{
    var count = (await rooms.GetRoomsAsync()).Count;
    Console.WriteLine($"Підключено до API {apiUrl}. Кімнат: {count}.");
    Console.WriteLine();
}
catch (HttpRequestException)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"API недоступне за адресою {apiUrl}.");
    Console.ResetColor();
    Console.WriteLine("Спочатку запустіть його в іншому вікні:  dotnet run --project SmartHomeSystem.API");
    return 1;
}

while (true)
{
    PrintMenu();
    var choice = Ask("Ваш вибір");
    Console.WriteLine();

    if (choice == "0")
    {
        Console.WriteLine("До побачення!");
        return 0;
    }

    try
    {
        switch (choice)
        {
            case "1": await ShowRoomsAsync(await rooms.GetRoomsAsync()); break;
            case "2": await RoomDetailsAsync(); break;
            case "3": await SearchRoomsAsync(); break;
            case "4": await CreateRoomAsync(); break;
            case "5": await DeleteRoomAsync(); break;
            case "6": ShowDevices(await devices.GetAllDevicesAsync()); break;
            case "7": await SearchDevicesAsync(); break;
            case "8": await AddDeviceAsync(); break;
            case "9": await ToggleDeviceAsync(); break;
            case "10": await AddTelemetryAsync(); break;
            case "11": await ShowTelemetryAsync(); break;
            case "12": await SimulateAsync(); break;
            default: Warn("Невідомий пункт меню."); break;
        }
    }
    catch (Exception ex) when (ex is ApiException or NotFoundException or DuplicateRoomNameException
                                  or RequestValidationException or DomainValidationException)
    {
        Fail(ex.Message);
    }
    catch (InvalidOperationException ex)
    {
        Fail(ex.Message);
    }
    catch (HttpRequestException)
    {
        Fail($"втрачено зв'язок з API ({apiUrl}). Перевірте, чи воно запущене.");
    }

    Console.WriteLine();
    Console.Write("Натисніть Enter, щоб продовжити...");
    Console.ReadLine();
    Console.Clear();
}

// ====================== Пункти меню ======================

async Task RoomDetailsAsync()
{
    var room = await PickRoomAsync();
    if (room is null) return;

    var details = await rooms.GetRoomDetailsAsync(room.Id); // повертає null, якщо кімнати немає
    if (details is null)
    {
        Warn("Кімнату не знайдено (404).");
        return;
    }

    Console.WriteLine($"Кімната:        {details.Name}");
    Console.WriteLine($"Id:             {details.Id}");
    Console.WriteLine($"Тип / поверх:   {details.Type} / {details.Floor}");
    Console.WriteLine($"Статус:         {StatusText(details.Status)}");
    Console.WriteLine($"Бажана t:       {details.TargetTemperature} °C");
    Console.WriteLine($"Остання синхр.: {(details.MeasurementsCount == 0 ? "—" : details.LastSync.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss"))}");
    Console.WriteLine($"Пристроїв:      {details.DevicesCount}, вимірювань: {details.MeasurementsCount}");
}

async Task SearchRoomsAsync()
{
    var query = Ask("Пошук кімнати за назвою (порожньо = усі)");
    var found = await rooms.SearchRoomsAsync(query);
    if (found.Count == 0) Warn("Збігів немає — повернувся порожній список.");
    else await ShowRoomsAsync(found);
}

async Task CreateRoomAsync()
{
    var name = Ask("Назва");
    var type = AskEnum<RoomType>("Тип кімнати");
    var floor = AskInt("Поверх (0–10)");
    var target = AskInt("Бажана температура, °C (10–30)");

    var created = await rooms.CreateRoomAsync(
        new CreateRoomDto { Name = name, Type = type, Floor = floor, TargetTemperature = target });
    Ok($"Кімнату «{created.Name}» створено (Id {created.Id}).");
}

async Task DeleteRoomAsync()
{
    var room = await PickRoomAsync();
    if (room is null) return;

    await rooms.DeleteRoomAsync(room.Id);
    Ok($"Кімнату «{room.Name}» видалено.");
}

async Task SearchDevicesAsync()
{
    var query = Ask("Пошук пристрою за назвою (порожньо = усі)");
    var found = await devices.SearchDevicesAsync(query);
    if (found.Count == 0) Warn("Збігів немає — повернувся порожній список.");
    else ShowDevices(found);
}

async Task AddDeviceAsync()
{
    var room = await PickRoomAsync();
    if (room is null) return;

    var name = Ask("Назва пристрою");
    var type = AskEnum<DeviceType>("Тип пристрою");

    var created = await devices.AddDeviceAsync(room.Id, new CreateDeviceDto { Name = name, Type = type });
    Ok($"Пристрій «{created.Name}» додано до «{created.RoomName}». Стан: {EnabledText(created.IsEnabled)} (новий пристрій завжди вимкнений).");
}

async Task ToggleDeviceAsync()
{
    var all = await devices.GetAllDevicesAsync();
    if (all.Count == 0) { Warn("Пристроїв немає."); return; }

    ShowDevices(all);
    var index = AskInt($"Номер пристрою (1–{all.Count})");
    if (index < 1 || index > all.Count) { Warn("Немає пристрою з таким номером."); return; }

    var device = all[index - 1];
    var result = device.IsEnabled
        ? await devices.DeactivateDeviceAsync(device.Id)
        : await devices.ActivateDeviceAsync(device.Id);

    Ok($"«{result.Name}»: {EnabledText(device.IsEnabled)} -> {EnabledText(result.IsEnabled)}");
}

async Task AddTelemetryAsync()
{
    var room = await PickRoomAsync();
    if (room is null) return;

    var t = AskDouble("Температура, °C (-50…60)");
    var h = AskDouble("Вологість, % (0…100)");
    var e = AskDouble("Споживання, кВт·год (0…100)");

    await RecordAndExplainAsync(room, t, h, e);
}

async Task ShowTelemetryAsync()
{
    var room = await PickRoomAsync();
    if (room is null) return;

    var list = await telemetry.GetTelemetryAsync(room.Id);
    if (list.Count == 0) { Warn($"У «{room.Name}» ще немає вимірювань."); return; }

    Console.WriteLine($"Вимірювання кімнати «{room.Name}»:");
    Console.WriteLine($"{"Id",3} | {"Час",-19} | {"t, °C",6} | {"Волог., %",9} | {"кВт·год",7}");
    Console.WriteLine(new string('-', 58));
    foreach (var m in list)
    {
        Console.WriteLine($"{m.Id,3} | {m.Timestamp.ToLocalTime():dd.MM.yyyy HH:mm:ss} | {m.Temperature,6:0.0} | {m.Humidity,9:0.0} | {m.EnergyConsumption,7:0.00}");
    }
}

// Показує «розумну» поведінку: що саме змінила автоматизація після вимірювання.
async Task SimulateAsync()
{
    var room = await PickRoomAsync();
    if (room is null) return;

    Console.WriteLine($"Симуляція для «{room.Name}» (бажана {room.TargetTemperature} °C): холодно -> тепло -> спека -> нормалізація");
    var target = room.TargetTemperature;
    var steps = new (double T, double H, double E)[]
    {
        (target - 4, 45, 0.6),
        (target + 1, 50, 0.7),
        (target + 7, 55, 1.4),
        (target,     72, 0.9),
        (target,     45, 0.5),
    };

    foreach (var (tempC, hum, energy) in steps)
    {
        Console.WriteLine();
        await RecordAndExplainAsync(room, tempC, hum, energy);
    }
}

// ====================== Допоміжне ======================

async Task RecordAndExplainAsync(RoomResponseDto room, double t, double h, double e)
{
    var devicesBefore = (await devices.GetAllDevicesAsync()).Where(d => d.RoomId == room.Id).ToList();
    var statusBefore = (await rooms.GetRoomDetailsAsync(room.Id))!.Status;

    var saved = await telemetry.AddTelemetryAsync(room.Id,
        new CreateTelemetryDto { Temperature = t, Humidity = h, EnergyConsumption = e });

    var devicesAfter = (await devices.GetAllDevicesAsync()).Where(d => d.RoomId == room.Id).ToList();
    var statusAfter = (await rooms.GetRoomDetailsAsync(room.Id))!.Status;

    Ok($"Вимірювання #{saved.Id}: {t:0.#} °C, {h:0.#} %, {e:0.##} кВт·год");
    Console.WriteLine($"   Статус кімнати: {StatusText(statusBefore)} -> {StatusText(statusAfter)}");

    var changed = false;
    foreach (var after in devicesAfter)
    {
        var before = devicesBefore.First(d => d.Id == after.Id);
        if (before.IsEnabled != after.IsEnabled)
        {
            changed = true;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"   АВТОМАТИКА: «{after.Name}» {EnabledText(before.IsEnabled)} -> {EnabledText(after.IsEnabled)}");
            Console.ResetColor();
        }
    }

    if (!changed) Console.WriteLine("   Автоматика: стан пристроїв не змінився.");
}

async Task<RoomResponseDto?> PickRoomAsync()
{
    var all = await rooms.GetRoomsAsync();
    if (all.Count == 0) { Warn("Кімнат немає."); return null; }

    await ShowRoomsAsync(all);
    var index = AskInt($"Номер кімнати (1–{all.Count})");
    if (index < 1 || index > all.Count) { Warn("Немає кімнати з таким номером."); return null; }

    return all[index - 1];
}

Task ShowRoomsAsync(IReadOnlyList<RoomResponseDto> list)
{
    Console.WriteLine($"{"№",2} | {"Назва",-14} | {"Тип",-10} | {"Пов.",4} | {"Статус",-12} | {"Ціль",4} | {"Прист.",6} | {"Вимір.",6}");
    Console.WriteLine(new string('-', 84));
    for (var i = 0; i < list.Count; i++)
    {
        var r = list[i];
        Console.WriteLine($"{i + 1,2} | {r.Name,-14} | {r.Type,-10} | {r.Floor,4} | {StatusText(r.Status),-12} | {r.TargetTemperature,4} | {r.DevicesCount,6} | {r.MeasurementsCount,6}");
    }

    return Task.CompletedTask;
}

void ShowDevices(IReadOnlyList<DeviceDto> list)
{
    Console.WriteLine($"{"№",2} | {"Назва",-20} | {"Тип",-14} | {"Кімната",-12} | Стан");
    Console.WriteLine(new string('-', 70));
    for (var i = 0; i < list.Count; i++)
    {
        var d = list[i];
        Console.WriteLine($"{i + 1,2} | {d.Name,-20} | {d.Type,-14} | {d.RoomName,-12} | {EnabledText(d.IsEnabled)}");
    }
}

void PrintMenu()
{
    Console.WriteLine("=============== РОЗУМНИЙ БУДИНОК ===============");
    Console.WriteLine(" КІМНАТИ                    ПРИСТРОЇ");
    Console.WriteLine("  1. Список кімнат           6. Список пристроїв");
    Console.WriteLine("  2. Деталі кімнати          7. Пошук пристроїв за назвою");
    Console.WriteLine("  3. Пошук за назвою         8. Додати пристрій у кімнату");
    Console.WriteLine("  4. Створити кімнату        9. Увімкнути / вимкнути пристрій");
    Console.WriteLine("  5. Видалити кімнату");
    Console.WriteLine(" ВИМІРЮВАННЯ ТА АВТОМАТИКА");
    Console.WriteLine(" 10. Додати вимірювання (спрацюють правила автоматики)");
    Console.WriteLine(" 11. Історія вимірювань кімнати");
    Console.WriteLine(" 12. Симуляція: холодно -> спека -> норма");
    Console.WriteLine("  0. Вихід");
    Console.WriteLine("================================================");
}

string Ask(string prompt)
{
    Console.Write($"{prompt}: ");
    return Console.ReadLine()?.Trim() ?? string.Empty;
}

int AskInt(string prompt)
{
    while (true)
    {
        if (int.TryParse(Ask(prompt), out var value)) return value;
        Warn("Введіть ціле число.");
    }
}

double AskDouble(string prompt)
{
    while (true)
    {
        var text = Ask(prompt).Replace(',', '.');
        if (double.TryParse(text, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var value)) return value;
        Warn("Введіть число (наприклад 22.5).");
    }
}

TEnum AskEnum<TEnum>(string prompt) where TEnum : struct, Enum
{
    var names = Enum.GetNames<TEnum>();
    Console.WriteLine($"{prompt}: " + string.Join(", ", names.Select((item, i) => $"{i + 1}={item}")));
    while (true)
    {
        var text = Ask("Номер або назва");
        if (int.TryParse(text, out var n) && n >= 1 && n <= names.Length) return Enum.Parse<TEnum>(names[n - 1]);
        if (Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)) return parsed;
        Warn("Невідоме значення.");
    }
}

string StatusText(RoomStatus s) => s switch
{
    RoomStatus.Vacant => "Порожньо",
    RoomStatus.Occupied => "Зайнято",
    RoomStatus.Warning => "УВАГА",
    RoomStatus.Alarm => "ТРИВОГА",
    _ => s.ToString()
};

string EnabledText(bool on) => on ? "УВІМКНЕНО" : "вимкнено";

void Ok(string text) => Colored(ConsoleColor.Green, text);
void Warn(string text) => Colored(ConsoleColor.Yellow, text);
void Fail(string text) => Colored(ConsoleColor.Red, "Помилка: " + text);

void Colored(ConsoleColor color, string text)
{
    Console.ForegroundColor = color;
    Console.WriteLine(text);
    Console.ResetColor();
}
