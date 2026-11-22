using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using SmartHomeSystem.Services.DTOs;
using SmartHomeSystem.Services.Interfaces;

namespace SmartHomeSystem.API.Controllers;

/// <summary>Керування пристроями розумного будинку (світло, термостати, замки, датчики).</summary>
/// <remarks>
/// Контролер не містить бізнес-логіки: він лише приймає HTTP-запит, викликає <see cref="IDeviceService"/>
/// і перетворює результат на HTTP-відповідь. Токен [controller] дає базовий маршрут api/devices.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DevicesController : ControllerBase
{
    private readonly IDeviceService _deviceService;
    private readonly IValidator<CreateDeviceRequest> _createValidator;
    private readonly IValidator<UpdateDeviceRequest> _updateValidator;
    private readonly IValidator<SetDeviceStatusRequest> _statusValidator;

    public DevicesController(
        IDeviceService deviceService,
        IValidator<CreateDeviceRequest> createValidator,
        IValidator<UpdateDeviceRequest> updateValidator,
        IValidator<SetDeviceStatusRequest> statusValidator)
    {
        _deviceService = deviceService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _statusValidator = statusValidator;
    }

    // Запускає FluentValidation-валідатор; за помилок повертає 400 (application/problem+json) зі списком помилок по полях.
    private async Task<ActionResult?> ValidateAsync<T>(IValidator<T> validator, T request)
    {
        var result = await validator.ValidateAsync(request);
        if (result.IsValid)
        {
            return null;
        }

        var errors = result.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());

        var problem = new ValidationProblemDetails(errors) { Instance = Request.Path };
        problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
        return BadRequest(problem);
    }

    /// <summary>Повертає список пристроїв, за потреби відфільтрований за назвою.</summary>
    /// <param name="name">Частина назви без урахування регістру, наприклад «?name=термо». Порожнє значення — усі пристрої.</param>
    /// <returns>200 OK зі списком (порожній список — теж 200).</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DeviceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DeviceDto>>> GetAll([FromQuery] string? name)
        => Ok(await _deviceService.SearchDevicesAsync(name ?? string.Empty));

    /// <summary>Пошук за назвою (маршрут із Lab3; зберігається для консольного клієнта).</summary>
    /// <param name="query">Частина назви без урахування регістру.</param>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyList<DeviceDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DeviceDto>>> Search([FromQuery] string? query)
        => Ok(await _deviceService.SearchDevicesAsync(query ?? string.Empty));

    /// <summary>Повертає пристрій за ідентифікатором.</summary>
    /// <param name="id">Унікальний ідентифікатор пристрою (GUID).</param>
    /// <returns>200 OK з пристроєм або 404 Not Found, якщо його немає.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DeviceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceDto>> GetById(Guid id)
    {
        var device = await _deviceService.GetDeviceDetailsAsync(id);
        return device is null ? NotFound() : Ok(device);
    }

    /// <summary>Створює новий пристрій у вказаній кімнаті (новий пристрій вимкнений).</summary>
    /// <param name="request">Назва, тип і room_id кімнати.</param>
    /// <returns>201 Created із посиланням на ресурс (заголовок Location); 400 — некоректні дані; 404 — кімнати немає.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(DeviceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceDto>> Create([FromBody] CreateDeviceRequest request)
    {
        var invalid = await ValidateAsync(_createValidator, request);
        if (invalid is not null)
        {
            return invalid;
        }

        var created = await _deviceService.CreateDeviceAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Повне оновлення пристрою (PUT): назва й тип.</summary>
    /// <param name="id">Унікальний ідентифікатор пристрою (GUID).</param>
    /// <param name="request">Нова назва й тип пристрою.</param>
    /// <returns>200 OK з оновленим пристроєм; 400 — некоректні дані; 404 — пристрою немає.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(DeviceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceDto>> Update(Guid id, [FromBody] UpdateDeviceRequest request)
    {
        var invalid = await ValidateAsync(_updateValidator, request);
        if (invalid is not null)
        {
            return invalid;
        }

        var updated = await _deviceService.UpdateDeviceAsync(id, request);
        return updated is null ? NotFound() : Ok(updated);
    }

    /// <summary>Часткове оновлення: вмикає або вимикає пристрій.</summary>
    /// <param name="id">Унікальний ідентифікатор пристрою (GUID).</param>
    /// <param name="request">Об'єкт з бажаним станом (is_active).</param>
    /// <returns>204 No Content у разі успіху; 404 Not Found, якщо пристрою немає.</returns>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] SetDeviceStatusRequest request)
    {
        var invalid = await ValidateAsync(_statusValidator, request);
        if (invalid is not null)
        {
            return invalid;
        }

        var found = await _deviceService.SetDeviceStateAsync(id, request.IsActive!.Value);
        return found ? NoContent() : NotFound();
    }

    /// <summary>Видаляє пристрій із сховища.</summary>
    /// <param name="id">Унікальний ідентифікатор пристрою (GUID).</param>
    /// <returns>204 No Content у разі успіху; 404 Not Found, якщо пристрою немає.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
        => await _deviceService.DeleteDeviceAsync(id) ? NoContent() : NotFound();

    // ----- Маршрути з Lab3 (використовує консольний клієнт); збережено для сумісності -----

    /// <summary>Додає пристрій у кімнату (маршрут із Lab3). Рекомендовано використовувати POST /api/devices.</summary>
    /// <param name="roomId">Ідентифікатор кімнати.</param>
    /// <param name="dto">Назва й тип пристрою.</param>
    [HttpPost("/api/rooms/{roomId:guid}/devices")]
    [ProducesResponseType(typeof(DeviceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceDto>> CreateInRoom(Guid roomId, [FromBody] CreateDeviceDto dto)
    {
        var created = await _deviceService.AddDeviceAsync(roomId, dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Вмикає пристрій (маршрут із Lab3). Рекомендовано використовувати PATCH .../status.</summary>
    /// <param name="id">Ідентифікатор пристрою.</param>
    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(typeof(DeviceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceDto>> Activate(Guid id)
        => Ok(await _deviceService.ActivateDeviceAsync(id));

    /// <summary>Вимикає пристрій (маршрут із Lab3). Рекомендовано використовувати PATCH .../status.</summary>
    /// <param name="id">Ідентифікатор пристрою.</param>
    [HttpPost("{id:guid}/deactivate")]
    [ProducesResponseType(typeof(DeviceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeviceDto>> Deactivate(Guid id)
        => Ok(await _deviceService.DeactivateDeviceAsync(id));
}
