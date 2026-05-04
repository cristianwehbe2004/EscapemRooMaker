using System.Security.Claims;
using EscapeRoom.Application.Rooms;
using EscapeRoom.Application.Rooms.Contracts;
using EscapeRoom.Domain.Entities;
using EscapeRoom.Domain.Enums;
using EscapeRoom.Infrastructure.Data;
using EscapeRoom.TriggerEngine.Validation;
using Microsoft.EntityFrameworkCore;

namespace EscapeRoom.Infrastructure.Rooms;

public class CreatorRoomService(
    AppDbContext dbContext,
    ITriggerGraphValidator graphValidator) : ICreatorRoomService
{
    public async Task<EditorDocumentDto> GetEditorDocumentAsync(Guid roomId, Guid actorUserId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var room = await GetAuthorizedRoomAsync(roomId, actorUserId, isAdmin, cancellationToken);
        return EditorDocumentMapper.Deserialize(room.GraphDefinition);
    }

    public async Task<ValidateRoomResponse> ValidateAsync(Guid roomId, EditorDocumentDto document, Guid actorUserId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        _ = await GetAuthorizedRoomAsync(roomId, actorUserId, isAdmin, cancellationToken);
        var issues = EditorDocumentValidator.Validate(document, graphValidator);
        return new ValidateRoomResponse
        {
            IsValid = issues.Count == 0,
            Issues = issues
        };
    }

    public async Task<SaveRoomResponse> SaveAsync(Guid roomId, EditorDocumentDto document, Guid actorUserId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var room = await GetAuthorizedRoomAsync(roomId, actorUserId, isAdmin, cancellationToken);
        var issues = EditorDocumentValidator.Validate(document, graphValidator);
        if (issues.Count > 0)
        {
            return new SaveRoomResponse
            {
                RoomId = roomId,
                VersionNumber = await GetLatestVersionNumberAsync(roomId, cancellationToken),
                SavedAtUtc = DateTime.UtcNow,
                Issues = issues
            };
        }

        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var nextVersion = await GetLatestVersionNumberAsync(roomId, cancellationToken) + 1;
        var serialized = EditorDocumentMapper.Serialize(document);

        room.GraphDefinition = serialized;

        dbContext.RoomVersions.Add(new RoomVersion
        {
            RoomId = room.Id,
            VersionNumber = nextVersion,
            GraphDefinition = serialized,
            CreatedAtUtc = DateTime.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new SaveRoomResponse
        {
            RoomId = room.Id,
            VersionNumber = nextVersion,
            SavedAtUtc = DateTime.UtcNow,
            Issues = []
        };
    }

    public async Task<CreatePlaytestSessionResponse> CreatePlaytestSessionAsync(Guid roomId, Guid actorUserId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var room = await GetAuthorizedRoomAsync(roomId, actorUserId, isAdmin, cancellationToken);
        var session = new GameSession
        {
            RoomId = room.Id,
            Status = SessionStatus.Pending,
            StateSnapshot = BuildInitialSessionState(room.GraphDefinition),
            StartedAtUtc = DateTime.UtcNow
        };

        dbContext.Sessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreatePlaytestSessionResponse
        {
            SessionId = session.Id,
            PlayerJoinPath = $"/player?sessionId={session.Id}",
            GmJoinPath = $"/gm?sessionId={session.Id}"
        };
    }

    public async Task<PublishRoomResponse> PublishAsync(Guid roomId, Guid actorUserId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var room = await GetAuthorizedRoomAsync(roomId, actorUserId, isAdmin, cancellationToken);
        room.IsPublished = true;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new PublishRoomResponse
        {
            RoomId = room.Id,
            IsPublished = room.IsPublished,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private static string BuildInitialSessionState(string graphDefinition)
    {
        var document = EditorDocumentMapper.Deserialize(graphDefinition);
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            room = document.Room,
            inventory = Array.Empty<object>(),
            messages = new[] { "Playtest session started." }
        });
    }

    private async Task<Room> GetAuthorizedRoomAsync(Guid roomId, Guid actorUserId, bool isAdmin, CancellationToken cancellationToken)
    {
        var room = await dbContext.Rooms.FirstOrDefaultAsync(x => x.Id == roomId, cancellationToken)
            ?? throw new InvalidOperationException($"Room '{roomId}' was not found.");

        if (!isAdmin && room.CreatedByUserId != actorUserId)
        {
            throw new UnauthorizedAccessException("You do not have access to this room.");
        }

        return room;
    }

    private async Task<int> GetLatestVersionNumberAsync(Guid roomId, CancellationToken cancellationToken)
        => await dbContext.RoomVersions
            .Where(x => x.RoomId == roomId)
            .OrderByDescending(x => x.VersionNumber)
            .Select(x => (int?)x.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;
}
