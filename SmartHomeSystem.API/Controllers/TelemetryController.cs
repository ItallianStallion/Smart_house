using Microsoft.AspNetCore.Mvc;
using SmartHomeSystem.Services.DTOs;
using SmartHomeSystem.Services.Interfaces;

namespace SmartHomeSystem.API.Controllers;

[ApiController]
[Route("api/rooms/{roomId:guid}/telemetry")]
public class TelemetryController : ControllerBase
{
    private readonly ITelemetryService _telemetryService;

    public TelemetryController(ITelemetryService telemetryService) => _telemetryService = telemetryService;

    // GET api/rooms/{roomId}/telemetry
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TelemetryResponseDto>>> GetAll(Guid roomId)
        => Ok(await _telemetryService.GetTelemetryAsync(roomId));

    // POST api/rooms/{roomId}/telemetry  -> 201 Created
    [HttpPost]
    public async Task<ActionResult<TelemetryResponseDto>> Create(Guid roomId, CreateTelemetryDto dto)
    {
        var created = await _telemetryService.AddTelemetryAsync(roomId, dto);
        return CreatedAtAction(nameof(GetAll), new { roomId }, created);
    }

    // PUT api/rooms/{roomId}/telemetry/{telemetryId}
    [HttpPut("{telemetryId:long}")]
    public async Task<ActionResult<TelemetryResponseDto>> Update(
        Guid roomId, long telemetryId, CreateTelemetryDto dto)
        => Ok(await _telemetryService.UpdateTelemetryAsync(roomId, telemetryId, dto));

    // DELETE api/rooms/{roomId}/telemetry/{telemetryId}  -> 204 No Content
    [HttpDelete("{telemetryId:long}")]
    public async Task<IActionResult> Delete(Guid roomId, long telemetryId)
    {
        await _telemetryService.DeleteTelemetryAsync(roomId, telemetryId);
        return NoContent();
    }
}
