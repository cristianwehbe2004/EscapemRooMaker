using EscapeRoom.Application.Rooms.Contracts;

namespace EscapeRoom.Application.Rooms;

public interface ICreatorRoomService
{
    Task<EditorDocumentDto> GetEditorDocumentAsync(Guid roomId, Guid actorUserId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<ValidateRoomResponse> ValidateAsync(Guid roomId, EditorDocumentDto document, Guid actorUserId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<SaveRoomResponse> SaveAsync(Guid roomId, EditorDocumentDto document, Guid actorUserId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<CreatePlaytestSessionResponse> CreatePlaytestSessionAsync(Guid roomId, Guid actorUserId, bool isAdmin, CancellationToken cancellationToken = default);
}
