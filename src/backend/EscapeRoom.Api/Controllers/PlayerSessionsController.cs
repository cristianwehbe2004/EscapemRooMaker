using System.Security.Claims;
using EscapeRoom.Application.Sessions;
using EscapeRoom.Application.Sessions.Contracts;
using Microsoft.AspNetCore.Authorization;
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
        var response = await playerSessionService.CreateSessionAsync(request, ResolveIdentity(request.DisplayName, null), cancellationToken);
        return Ok(response);
    }

    [HttpPost("quick-start")]
    public async Task<ActionResult<PlayerSessionSummary>> QuickStart(
        [FromBody] CreateSessionRequest request,
        CancellationToken cancellationToken)
    {
        var response = await playerSessionService.QuickStartAsync(request, ResolveIdentity(request.DisplayName, null), cancellationToken);
        return Ok(response);
    }

    [HttpPost("{sessionId:guid}/join")]
    public async Task<ActionResult<PlayerSessionSummary>> JoinSession(
        Guid sessionId,
        [FromBody] JoinSessionRequest request,
        CancellationToken cancellationToken)
    {
        var response = await playerSessionService.JoinSessionAsync(
            sessionId,
            request,
            ResolveIdentity(request.DisplayName, request.GuestActorId),
            cancellationToken);
        return Ok(response);
    }

    [HttpPost("{sessionId:guid}/start")]
    public async Task<ActionResult<PlayerSessionSummary>> StartSession(
        Guid sessionId,
        [FromBody] JoinSessionRequest request,
        CancellationToken cancellationToken)
    {
        var response = await playerSessionService.StartSessionAsync(
            sessionId,
            ResolveIdentity(request.DisplayName, request.GuestActorId),
            cancellationToken);
        return Ok(response);
    }

    [HttpGet("{sessionId:guid}")]
    public async Task<ActionResult<PlayerSessionSummary>> GetSession(
        Guid sessionId,
        [FromQuery] string? displayName,
        [FromQuery] string? guestActorId,
        CancellationToken cancellationToken)
    {
        var response = await playerSessionService.GetSessionAsync(
            sessionId,
            ResolveIdentity(displayName, guestActorId),
            cancellationToken);
        return Ok(response);
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
}
