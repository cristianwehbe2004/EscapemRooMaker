namespace EscapeRoom.Application.Sessions;

public class SessionServiceException(string message) : InvalidOperationException(message);

public class SessionNotFoundException(Guid sessionId) : SessionServiceException($"Session '{sessionId}' was not found.");

public class RoomNotFoundException(Guid roomId) : SessionServiceException($"Room '{roomId}' was not found.");

public class PublishedRoomNotFoundException(Guid roomId)
    : SessionServiceException($"Published room '{roomId}' was not found.");

public class NoPublishedRoomAvailableException()
    : SessionServiceException("No published room is available to play.");

public class SessionAccessDeniedException(string message) : SessionServiceException(message);
