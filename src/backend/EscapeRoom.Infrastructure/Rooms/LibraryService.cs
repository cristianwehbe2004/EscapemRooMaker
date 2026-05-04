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

        var projected = roomsQuery.Select(room => new LibraryRoomListItemDto
        {
            RoomId = room.Id,
            Name = room.Name,
            Description = room.Description,
            CreatedAtUtc = room.CreatedAtUtc,
            RatingCount = dbContext.RoomRatings.Count(r => r.RoomId == room.Id),
            AverageRating = Math.Round(
                dbContext.RoomRatings
                    .Where(r => r.RoomId == room.Id)
                    .Select(r => (double?)r.Score)
                    .Average() ?? 0,
                2),
            ViewerRating = null
        });

        projected = NormalizeSort(projected, sort);

        var total = await projected.CountAsync(cancellationToken);
        var items = await projected
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        if (viewerUserId.HasValue && items.Count > 0)
        {
            var roomIds = items.Select(x => x.RoomId).ToList();
            var viewerRatings = await dbContext.RoomRatings
                .AsNoTracking()
                .Where(x => x.UserId == viewerUserId.Value && roomIds.Contains(x.RoomId))
                .ToDictionaryAsync(x => x.RoomId, x => x.Score, cancellationToken);

            foreach (var item in items)
            {
                if (viewerRatings.TryGetValue(item.RoomId, out var score))
                {
                    item.ViewerRating = score;
                }
            }
        }

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

    private static IQueryable<LibraryRoomListItemDto> NormalizeSort(IQueryable<LibraryRoomListItemDto> query, string? sort)
    {
        var mode = sort?.Trim().ToLowerInvariant();
        return mode switch
        {
            "name" => query.OrderBy(x => x.Name).ThenByDescending(x => x.CreatedAtUtc),
            "rating" => query.OrderByDescending(x => x.AverageRating).ThenByDescending(x => x.RatingCount).ThenBy(x => x.Name),
            _ => query.OrderByDescending(x => x.CreatedAtUtc)
        };
    }
}
