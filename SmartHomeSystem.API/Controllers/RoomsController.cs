using Microsoft.AspNetCore.Mvc;
using SmartHomeSystem.Services.DTOs;
using SmartHomeSystem.Services.Interfaces;

namespace SmartHomeSystem.API.Controllers;

[ApiController]
[Route("api/rooms")]
public class RoomsController : ControllerBase
{
    private readonly IRoomService _roomService;

    public RoomsController(IRoomService roomService) => _roomService = roomService;

    // GET api/rooms
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoomResponseDto>>> GetAll()
        => Ok(await _roomService.GetRoomsAsync());

    // GET api/rooms/search?query=кухня  (без урахування регістру)
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<RoomResponseDto>>> Search([FromQuery] string? query)
        => Ok(await _roomService.SearchRoomsAsync(query ?? string.Empty));

    // GET api/rooms/{id}  -> 200 OK або 404 Not Found
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RoomResponseDto>> GetById(Guid id)
    {
        var room = await _roomService.GetRoomDetailsAsync(id);
        return room is null ? NotFound() : Ok(room);
    }

    // POST api/rooms  -> 201 Created
    [HttpPost]
    public async Task<ActionResult<RoomResponseDto>> Create(CreateRoomDto dto)
    {
        var created = await _roomService.CreateRoomAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    // PUT api/rooms/{id}
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RoomResponseDto>> Update(Guid id, CreateRoomDto dto)
        => Ok(await _roomService.UpdateRoomAsync(id, dto));

    // DELETE api/rooms/{id}  -> 204 No Content
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _roomService.DeleteRoomAsync(id);
        return NoContent();
    }
}
