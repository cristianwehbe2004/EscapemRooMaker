using System.Text.Json;
using EscapeRoom.Application.Rooms;
using EscapeRoom.Application.Rooms.Contracts;
using EscapeRoom.Domain.Entities;
using EscapeRoom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscapeRoom.Infrastructure.Rooms;

public class LibraryService(AppDbContext dbContext) : ILibraryService
{
    public async Task<LibraryRoomsResponse> GetPublishedRoomsAsync(
        string? query,
        string? sort,
        bool? featuredOnly,
        int page,
        int pageSize,
        Guid? viewerUserId,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var roomsQuery = dbContext.Rooms
            .AsNoTracking()
            .Where(x => x.IsPublished);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var search = query.Trim().ToLower();
            roomsQuery = roomsQuery.Where(x =>
                x.Name.ToLower().Contains(search) ||
                x.Description.ToLower().Contains(search));
        }

        var rooms = await roomsQuery.ToListAsync(cancellationToken);
        if (rooms.Count == 0)
        {
            return new LibraryRoomsResponse
            {
                Items = [],
                Page = page,
                PageSize = pageSize,
                Total = 0
            };
        }

        var roomIds = rooms.Select(x => x.Id).ToList();
        var ratingAggregate = await dbContext.RoomRatings
            .AsNoTracking()
            .Where(x => roomIds.Contains(x.RoomId))
            .GroupBy(x => x.RoomId)
            .Select(g => new
            {
                RoomId = g.Key,
                Count = g.Count(),
                Average = g.Average(x => (double)x.Score)
            })
            .ToDictionaryAsync(x => x.RoomId, x => new { x.Count, x.Average }, cancellationToken);

        Dictionary<Guid, int> viewerRatings = [];
        if (viewerUserId.HasValue)
        {
            viewerRatings = await dbContext.RoomRatings
                .AsNoTracking()
                .Where(x => x.UserId == viewerUserId.Value && roomIds.Contains(x.RoomId))
                .ToDictionaryAsync(x => x.RoomId, x => x.Score, cancellationToken);
        }

        var projected = rooms.Select(room =>
        {
            var metadata = ResolveRoomMetadata(room.GraphDefinition);
            var aggregate = ratingAggregate.GetValueOrDefault(room.Id);
            return new LibraryRoomListItemDto
            {
                RoomId = room.Id,
                Name = room.Name,
                Description = room.Description,
                CreatedAtUtc = room.CreatedAtUtc,
                RatingCount = aggregate?.Count ?? 0,
                AverageRating = Math.Round(aggregate?.Average ?? 0, 2),
                ViewerRating = viewerRatings.TryGetValue(room.Id, out var score) ? score : null,
                IsFeatured = metadata.IsFeatured,
                Difficulty = metadata.Difficulty,
                EstimatedMinutes = metadata.EstimatedMinutes
            };
        });

        if (featuredOnly.HasValue)
        {
            projected = projected.Where(x => x.IsFeatured == featuredOnly.Value);
        }

        var sorted = NormalizeSort(projected, sort);
        var total = sorted.Count();
        var items = sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new LibraryRoomsResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            Total = total
        };
    }

    public async Task<UpsertRoomRatingResponse> UpsertRoomRatingAsync(
        Guid roomId,
        int score,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (score is < 1 or > 5)
        {
            throw new InvalidOperationException("Score must be between 1 and 5.");
        }

        var room = await dbContext.Rooms.FirstOrDefaultAsync(x => x.Id == roomId, cancellationToken)
            ?? throw new InvalidOperationException($"Room '{roomId}' was not found.");

        if (!room.IsPublished)
        {
            throw new InvalidOperationException("Only published rooms can be rated.");
        }

        var rating = await dbContext.RoomRatings
            .FirstOrDefaultAsync(x => x.RoomId == roomId && x.UserId == actorUserId, cancellationToken);

        if (rating is null)
        {
            dbContext.RoomRatings.Add(new RoomRating
            {
                RoomId = roomId,
                UserId = actorUserId,
                Score = score,
                CreatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            rating.Score = score;
            rating.CreatedAtUtc = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var aggregate = await dbContext.RoomRatings
            .Where(x => x.RoomId == roomId)
            .GroupBy(x => x.RoomId)
            .Select(g => new { Count = g.Count(), Average = g.Average(x => (double)x.Score) })
            .FirstAsync(cancellationToken);

        return new UpsertRoomRatingResponse
        {
            RoomId = roomId,
            Score = score,
            RatingCount = aggregate.Count,
            AverageRating = Math.Round(aggregate.Average, 2)
        };
    }

    public async Task<UnpublishRoomResponse> UnpublishAsync(
        Guid roomId,
        CancellationToken cancellationToken = default)
    {
        var room = await dbContext.Rooms.FirstOrDefaultAsync(x => x.Id == roomId, cancellationToken)
            ?? throw new InvalidOperationException($"Room '{roomId}' was not found.");

        room.IsPublished = false;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new UnpublishRoomResponse
        {
            RoomId = room.Id,
            IsPublished = room.IsPublished,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private static IEnumerable<LibraryRoomListItemDto> NormalizeSort(IEnumerable<LibraryRoomListItemDto> entries, string? sort)
    {
        var mode = sort?.Trim().ToLowerInvariant();
        return mode switch
        {
            "name" => entries.OrderBy(x => x.Name).ThenByDescending(x => x.CreatedAtUtc),
            "rating" => entries.OrderByDescending(x => x.AverageRating).ThenByDescending(x => x.RatingCount).ThenBy(x => x.Name),
            _ => entries.OrderByDescending(x => x.CreatedAtUtc)
        };
    }

    private static RoomMetadata ResolveRoomMetadata(string graphDefinition)
    {
        if (string.IsNullOrWhiteSpace(graphDefinition))
        {
            return RoomMetadata.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(graphDefinition);
            var root = document.RootElement;
            var metadata = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("triggerGraph", out var triggerGraph)
                ? ExtractMetadata(triggerGraph)
                : ExtractMetadata(root);
            return metadata;
        }
        catch
        {
            return RoomMetadata.Empty;
        }
    }

    private static RoomMetadata ExtractMetadata(JsonElement graphRoot)
    {
        if (graphRoot.ValueKind != JsonValueKind.Object || !graphRoot.TryGetProperty("metadata", out var metadataNode) || metadataNode.ValueKind != JsonValueKind.Object)
        {
            return RoomMetadata.Empty;
        }

        var isFeatured = TryGetBool(metadataNode, "featured") || TryGetString(metadataNode, "featured")?.Equals("yes", StringComparison.OrdinalIgnoreCase) == true;
        var difficulty = TryGetString(metadataNode, "difficulty");
        var estimatedMinutes = TryGetInt(metadataNode, "estimatedMinutes") ?? TryGetInt(metadataNode, "estimated_minutes");

        return new RoomMetadata(isFeatured, difficulty, estimatedMinutes);
    }

    private static string? TryGetString(JsonElement source, string key)
        => source.TryGetProperty(key, out var node) && node.ValueKind == JsonValueKind.String
            ? node.GetString()
            : null;

    private static bool TryGetBool(JsonElement source, string key)
    {
        if (!source.TryGetProperty(key, out var node))
        {
            return false;
        }

        if (node.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (node.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        if (node.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = node.GetString();
        return string.Equals(text, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static int? TryGetInt(JsonElement source, string key)
    {
        if (!source.TryGetProperty(key, out var node))
        {
            return null;
        }

        if (node.ValueKind == JsonValueKind.Number && node.TryGetInt32(out var numeric))
        {
            return numeric;
        }

        if (node.ValueKind == JsonValueKind.String && int.TryParse(node.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private readonly record struct RoomMetadata(bool IsFeatured, string? Difficulty, int? EstimatedMinutes)
    {
        public static readonly RoomMetadata Empty = new(false, null, null);
    }
}
