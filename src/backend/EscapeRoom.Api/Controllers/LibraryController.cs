using System.Security.Claims;
using EscapeRoom.Application.Rooms;
using EscapeRoom.Application.Rooms.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EscapeRoom.Api.Controllers;

[ApiController]
[Route("api/library/rooms")]
public class LibraryController(ILibraryService libraryService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<LibraryRoomsResponse>> GetRooms(
        [FromQuery] string? q,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var viewer = TryResolveUserId();
        var response = await libraryService.GetPublishedRoomsAsync(q, sort, page, pageSize, viewer, cancellationToken);
        return Ok(response);
    }

    [Authorize]
    [HttpPut("{roomId:guid}/rating")]
    public async Task<ActionResult<UpsertRoomRatingResponse>> UpsertRating(
        Guid roomId,
        [FromBody] UpsertRoomRatingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = ResolveUserId();
            var response = await libraryService.UpsertRoomRatingAsync(roomId, request.Score, userId, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private Guid ResolveUserId()
    {
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(subject, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user identifier.");
        }

        return userId;
    }

    private Guid? TryResolveUserId()
    {
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (Guid.TryParse(subject, out var userId))
        {
            return userId;
        }

        return null;
    }
}
