using EscapeRoom.Infrastructure.Data;
using EscapeRoom.Infrastructure.Rooms;
using EscapeRoom.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EscapeRoom.Tests.Unit.Rooms;

public class LibraryServiceTests
{
    [Fact]
    public async Task GetPublishedRoomsAsync_ShouldApplySearchAndSortByRating()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"library-list-{Guid.NewGuid()}")
            .Options;

        var roomA = new Room { Id = Guid.NewGuid(), Name = "Alpha", Description = "first", IsPublished = true, CreatedByUserId = Guid.NewGuid(), GraphDefinition = "{}" };
        var roomB = new Room { Id = Guid.NewGuid(), Name = "Beta", Description = "second", IsPublished = true, CreatedByUserId = Guid.NewGuid(), GraphDefinition = "{}" };
        var roomC = new Room { Id = Guid.NewGuid(), Name = "Hidden", Description = "third", IsPublished = false, CreatedByUserId = Guid.NewGuid(), GraphDefinition = "{}" };

        await using (var seedContext = new AppDbContext(dbOptions))
        {
            seedContext.Rooms.AddRange(roomA, roomB, roomC);
            seedContext.RoomRatings.AddRange(
                new RoomRating { RoomId = roomA.Id, UserId = Guid.NewGuid(), Score = 4 },
                new RoomRating { RoomId = roomA.Id, UserId = Guid.NewGuid(), Score = 5 },
                new RoomRating { RoomId = roomB.Id, UserId = Guid.NewGuid(), Score = 3 });
            await seedContext.SaveChangesAsync();
        }

        await using var context = new AppDbContext(dbOptions);
        var service = new LibraryService(context);
        var response = await service.GetPublishedRoomsAsync("a", "rating", 1, 20, null);

        response.Total.Should().Be(2);
        response.Items.Should().HaveCount(2);
        response.Items[0].RoomId.Should().Be(roomA.Id);
        response.Items[0].AverageRating.Should().Be(4.5);
        response.Items[0].RatingCount.Should().Be(2);
    }

    [Fact]
    public async Task UpsertRoomRatingAsync_ShouldReplaceExistingRatingAndRecomputeAggregate()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"library-upsert-{Guid.NewGuid()}")
            .Options;
        var roomId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using (var seedContext = new AppDbContext(dbOptions))
        {
            seedContext.Rooms.Add(new Room
            {
                Id = roomId,
                Name = "Rateable",
                Description = "Room",
                IsPublished = true,
                CreatedByUserId = Guid.NewGuid(),
                GraphDefinition = "{}"
            });
            seedContext.RoomRatings.Add(new RoomRating { RoomId = roomId, UserId = userId, Score = 2 });
            await seedContext.SaveChangesAsync();
        }

        await using var context = new AppDbContext(dbOptions);
        var service = new LibraryService(context);

        var response = await service.UpsertRoomRatingAsync(roomId, 5, userId);
        response.Score.Should().Be(5);
        response.RatingCount.Should().Be(1);
        response.AverageRating.Should().Be(5);
    }

    [Fact]
    public async Task UnpublishAsync_ShouldHideRoomFromCatalog()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"library-unpublish-{Guid.NewGuid()}")
            .Options;
        var roomId = Guid.NewGuid();

        await using (var seedContext = new AppDbContext(dbOptions))
        {
            seedContext.Rooms.Add(new Room
            {
                Id = roomId,
                Name = "Public",
                Description = "Room",
                IsPublished = true,
                CreatedByUserId = Guid.NewGuid(),
                GraphDefinition = "{}"
            });
            await seedContext.SaveChangesAsync();
        }

        await using var context = new AppDbContext(dbOptions);
        var service = new LibraryService(context);
        await service.UnpublishAsync(roomId);

        var response = await service.GetPublishedRoomsAsync(null, "newest", 1, 20, null);
        response.Total.Should().Be(0);
    }
}
