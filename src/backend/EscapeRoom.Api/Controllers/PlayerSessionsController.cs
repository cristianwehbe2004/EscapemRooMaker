using System.Security.Claims;
using EscapeRoom.Application.Sessions;
using EscapeRoom.Application.Sessions.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EscapeRoom.Api.Controllers;

[ApiController]
[Route("api/player/sessions")]
[AllowAnonymous]
public class PlayerSessionsController(IPlayerSessionService playerSessionService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PlayerSessionSummary>> CreateSession(
        [FromBody] CreateSessionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await playerSessionService.CreateSessionAsync(
                request,
                ResolveIdentity(request.DisplayName, request.GuestActorId),
                cancellationToken);
            return Ok(response);
        }
        catch (Exception ex) when (TryMapSessionError(ex, out var mapped))
        {
            return mapped;
        }
    }

    [HttpPost("quick-start")]
    public async Task<ActionResult<PlayerSessionSummary>> QuickStart(
        [FromBody] CreateSessionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await playerSessionService.QuickStartAsync(
                request,
                ResolveIdentity(request.DisplayName, request.GuestActorId),
                cancellationToken);
            return Ok(response);
        }
        catch (Exception ex) when (TryMapSessionError(ex, out var mapped))
        {
            return mapped;
        }
    }

    [HttpPost("{sessionId:guid}/join")]
    public async Task<ActionResult<PlayerSessionSummary>> JoinSession(
        Guid sessionId,
        [FromBody] JoinSessionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await playerSessionService.JoinSessionAsync(
                sessionId,
                request,
                ResolveIdentity(request.DisplayName, request.GuestActorId),
                cancellationToken);
            return Ok(response);
        }
        catch (Exception ex) when (TryMapSessionError(ex, out var mapped))
        {
            return mapped;
        }
    }

    [HttpPost("{sessionId:guid}/start")]
    public async Task<ActionResult<PlayerSessionSummary>> StartSession(
        Guid sessionId,
        [FromBody] JoinSessionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await playerSessionService.StartSessionAsync(
                sessionId,
                ResolveIdentity(request.DisplayName, request.GuestActorId),
                cancellationToken);
            return Ok(response);
        }
        catch (Exception ex) when (TryMapSessionError(ex, out var mapped))
        {
            return mapped;
        }
    }

    [HttpGet("{sessionId:guid}")]
    public async Task<ActionResult<PlayerSessionSummary>> GetSession(
        Guid sessionId,
        [FromQuery] string? displayName,
        [FromQuery] string? guestActorId,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await playerSessionService.GetSessionAsync(
                sessionId,
                ResolveIdentity(displayName, guestActorId),
                cancellationToken);
            return Ok(response);
        }
        catch (Exception ex) when (TryMapSessionError(ex, out var mapped))
        {
            return mapped;
        }
    }

    [HttpPost("{sessionId:guid}/kick")]
    public async Task<ActionResult<PlayerSessionSummary>> KickParticipant(
        Guid sessionId,
        [FromBody] KickSessionParticipantRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await playerSessionService.KickParticipantAsync(
                sessionId,
                request.TargetActorId,
                ResolveIdentity(request.DisplayName, request.GuestActorId),
                cancellationToken);
            return Ok(response);
        }
        catch (Exception ex) when (TryMapSessionError(ex, out var mapped))
        {
            return mapped;
        }
    }

    private PlayerIdentity ResolveIdentity(string? displayName, string? guestActorId)
    {
        var subject = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!string.IsNullOrWhiteSpace(subject))
        {
            return new PlayerIdentity
            {
                ActorId = subject,
                DisplayName = User.FindFirstValue(ClaimTypes.Name)
                    ?? User.FindFirstValue(ClaimTypes.Email)
                    ?? displayName
                    ?? "Player",
                IsAuthenticated = true
            };
        }

        var resolvedGuestActorId = string.IsNullOrWhiteSpace(guestActorId)
            ? $"guest-{Guid.NewGuid():N}"
            : guestActorId.Trim();

        return new PlayerIdentity
        {
            ActorId = resolvedGuestActorId,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Guest Player" : displayName.Trim(),
            IsAuthenticated = false
        };
    }

    private bool TryMapSessionError(Exception exception, out ActionResult mapped)
    {
        mapped = exception switch
        {
            SessionNotFoundException => BuildProblem(StatusCodes.Status404NotFound, "Session not found", exception.Message),
            RoomNotFoundException => BuildProblem(StatusCodes.Status404NotFound, "Room not found", exception.Message),
            PublishedRoomNotFoundException => BuildProblem(StatusCodes.Status404NotFound, "Published room not found", exception.Message),
            NoPublishedRoomAvailableException => BuildProblem(StatusCodes.Status404NotFound, "No published room available", exception.Message),
            SessionAccessDeniedException => BuildProblem(StatusCodes.Status403Forbidden, "Session access denied", exception.Message),
            SessionServiceException => BuildProblem(StatusCodes.Status400BadRequest, "Session request failed", exception.Message),
            UnauthorizedAccessException => BuildProblem(StatusCodes.Status403Forbidden, "Unauthorized", exception.Message),
            _ => null!
        };

        return mapped is not null;
    }

    private ObjectResult BuildProblem(int statusCode, string title, string detail)
        => StatusCode(statusCode, new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        });
}
