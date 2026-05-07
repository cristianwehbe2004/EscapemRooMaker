using EscapeRoom.Domain.Entities;
using EscapeRoom.Infrastructure.Data;
using EscapeRoom.Infrastructure.Rooms;
using EscapeRoom.TriggerEngine.Validation;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EscapeRoom.Tests.Integration.Rooms;

public class LibraryLifecycleFlowTests
{
    [Fact]
    public async Task PublishRateUnpublish_ShouldAppearThenDisappearFromLibrary()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var creatorId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        await using (var seedContext = new AppDbContext(options))
        {
            await seedContext.Database.EnsureCreatedAsync();
            seedContext.Rooms.Add(new Room
            {
                Id = roomId,
                Name = "Lifecycle Room",
                Description = "flow",
                IsPublished = false,
                CreatedByUserId = creatorId,
                GraphDefinition = "{}"
            });
            await seedContext.SaveChangesAsync();
        }

        await using var context = new AppDbContext(options);
        var creatorService = new CreatorRoomService(context, new TriggerGraphValidator());
        var libraryService = new LibraryService(context);

        await creatorService.PublishAsync(roomId, creatorId, isAdmin: false);

        var listAfterPublish = await libraryService.GetPublishedRoomsAsync("lifecycle", "newest", null, 1, 20, playerId);
        listAfterPublish.Total.Should().Be(1);

        var rated = await libraryService.UpsertRoomRatingAsync(roomId, 5, playerId);
        rated.AverageRating.Should().Be(5);
        rated.RatingCount.Should().Be(1);

        var listAfterRating = await libraryService.GetPublishedRoomsAsync("lifecycle", "rating", null, 1, 20, playerId);
        listAfterRating.Items.Should().ContainSingle();
        listAfterRating.Items[0].ViewerRating.Should().Be(5);
        listAfterRating.Items[0].AverageRating.Should().Be(5);

        await libraryService.UnpublishAsync(roomId);
        var listAfterUnpublish = await libraryService.GetPublishedRoomsAsync("lifecycle", "newest", null, 1, 20, playerId);
        listAfterUnpublish.Total.Should().Be(0);
    }
}
