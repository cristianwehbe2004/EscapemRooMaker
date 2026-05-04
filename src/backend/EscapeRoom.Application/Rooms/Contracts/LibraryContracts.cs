namespace EscapeRoom.Application.Rooms.Contracts;

public class LibraryRoomListItemDto
{
    public Guid RoomId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public int RatingCount { get; set; }
    public double AverageRating { get; set; }
    public int? ViewerRating { get; set; }
}

public class LibraryRoomsResponse
{
    public List<LibraryRoomListItemDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
}

public class PublishRoomResponse
{
    public Guid RoomId { get; set; }
    public bool IsPublished { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class UnpublishRoomResponse
{
    public Guid RoomId { get; set; }
    public bool IsPublished { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class UpsertRoomRatingRequest
{
    public int Score { get; set; }
}

public class UpsertRoomRatingResponse
{
    public Guid RoomId { get; set; }
    public int Score { get; set; }
    public int RatingCount { get; set; }
    public double AverageRating { get; set; }
}
