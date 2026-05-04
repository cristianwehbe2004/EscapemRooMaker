using EscapeRoom.Application.Rooms;
using EscapeRoom.Application.Rooms.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EscapeRoom.Api.Controllers;

[ApiController]
[Route("api/admin/rooms")]
[Authorize(Policy = "AdminOnly")]
public class AdminRoomsController(ILibraryService libraryService) : ControllerBase
{
    [HttpPost("{roomId:guid}/unpublish")]
    public async Task<ActionResult<UnpublishRoomResponse>> Unpublish(Guid roomId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await libraryService.UnpublishAsync(roomId, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
