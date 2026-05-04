using System.Security.Claims;
using EscapeRoom.Application.Rooms;
using EscapeRoom.Application.Rooms.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EscapeRoom.Api.Controllers;

[ApiController]
[Route("api/creator/rooms")]
[Authorize(Policy = "CreatorOnly")]
public class CreatorRoomsController(ICreatorRoomService creatorRoomService) : ControllerBase
{
    [HttpGet("{roomId:guid}/editor-document")]
    public async Task<ActionResult<EditorDocumentDto>> GetEditorDocument(Guid roomId, CancellationToken cancellationToken)
    {
        var (userId, isAdmin) = ResolveActor();
        var document = await creatorRoomService.GetEditorDocumentAsync(roomId, userId, isAdmin, cancellationToken);
        return Ok(document);
    }

    [HttpPost("{roomId:guid}/validate")]
    public async Task<ActionResult<ValidateRoomResponse>> Validate(Guid roomId, [FromBody] ValidateRoomRequest request, CancellationToken cancellationToken)
    {
        var (userId, isAdmin) = ResolveActor();
        var response = await creatorRoomService.ValidateAsync(roomId, request.Document, userId, isAdmin, cancellationToken);
        return Ok(response);
    }

    [HttpPut("{roomId:guid}")]
    public async Task<ActionResult<SaveRoomResponse>> Save(Guid roomId, [FromBody] SaveRoomRequest request, CancellationToken cancellationToken)
    {
        var (userId, isAdmin) = ResolveActor();
        var response = await creatorRoomService.SaveAsync(roomId, request.Document, userId, isAdmin, cancellationToken);
        if (response.Issues.Count > 0)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }

    [HttpPost("{roomId:guid}/playtest-sessions")]
    public async Task<ActionResult<CreatePlaytestSessionResponse>> CreatePlaytestSession(Guid roomId, CancellationToken cancellationToken)
    {
        var (userId, isAdmin) = ResolveActor();
        var response = await creatorRoomService.CreatePlaytestSessionAsync(roomId, userId, isAdmin, cancellationToken);
        return Ok(response);
    }

    [HttpPost("{roomId:guid}/publish")]
    public async Task<ActionResult<PublishRoomResponse>> Publish(Guid roomId, CancellationToken cancellationToken)
    {
        var (userId, isAdmin) = ResolveActor();
        var response = await creatorRoomService.PublishAsync(roomId, userId, isAdmin, cancellationToken);
        return Ok(response);
    }

    private (Guid userId, bool isAdmin) ResolveActor()
    {
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(subject, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user identifier.");
        }

        var isAdmin = User.Claims.Any(x =>
            x.Type == ClaimTypes.Role &&
            x.Value.Equals("Admin", StringComparison.OrdinalIgnoreCase));

        return (userId, isAdmin);
    }
}
