using EscapeRoom.Application.Rooms.Contracts;

namespace EscapeRoom.Application.Rooms;

public interface ILibraryService
{
    Task<LibraryRoomsResponse> GetPublishedRoomsAsync(
        string? query,
        string? sort,
        int page,
        int pageSize,
        Guid? viewerUserId,
        CancellationToken cancellationToken = default);

    Task<UpsertRoomRatingResponse> UpsertRoomRatingAsync(
        Guid roomId,
        int score,
        Guid actorUserId,
        CancellationToken cancellationToken = default);

    Task<UnpublishRoomResponse> UnpublishAsync(
        Guid roomId,
        CancellationToken cancellationToken = default);
}
