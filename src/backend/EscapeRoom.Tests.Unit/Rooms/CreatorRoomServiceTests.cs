using EscapeRoom.Application.Rooms.Contracts;
using EscapeRoom.Domain.Entities;
using EscapeRoom.Infrastructure.Data;
using EscapeRoom.Infrastructure.Rooms;
using EscapeRoom.TriggerEngine.Validation;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EscapeRoom.Tests.Unit.Rooms;

public class CreatorRoomServiceTests
{
    [Fact]
    public async Task SaveAsync_ShouldReturnIssuesAndNotCreateNewVersion_WhenDocumentIsInvalid()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"creator-room-save-invalid-{Guid.NewGuid()}")
            .Options;

        var creatorId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        await using (var seedContext = new AppDbContext(dbOptions))
        {
            seedContext.Rooms.Add(new Room
            {
                Id = roomId,
                CreatedByUserId = creatorId,
                Name = "Test Room",
                Description = "Test",
                GraphDefinition = "{}",
                CreatedAtUtc = DateTime.UtcNow
            });

            seedContext.RoomVersions.Add(new RoomVersion
            {
                RoomId = roomId,
                VersionNumber = 1,
                GraphDefinition = "{}",
                CreatedAtUtc = DateTime.UtcNow
            });

            await seedContext.SaveChangesAsync();
        }

        await using var context = new AppDbContext(dbOptions);
        var service = new CreatorRoomService(context, new TriggerGraphValidator());
        var invalidDocument = new EditorDocumentDto
        {
            Room = new VisualRoomDto
            {
                Width = 900,
                Height = 600,
                Hotspots = []
            },
            TriggerGraph = new()
            {
                Nodes = [],
                Edges = []
            }
        };

        var response = await service.SaveAsync(roomId, invalidDocument, creatorId, isAdmin: false);

        response.Issues.Should().NotBeEmpty();
        response.VersionNumber.Should().Be(1);

        var versions = await context.RoomVersions.Where(x => x.RoomId == roomId).ToListAsync();
        versions.Should().HaveCount(1);
        versions.Single().VersionNumber.Should().Be(1);
    }

    [Fact]
    public async Task PublishAsync_ShouldAllowOwnerCreator()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"creator-room-publish-owner-{Guid.NewGuid()}")
            .Options;
        var creatorId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        await using (var seedContext = new AppDbContext(dbOptions))
        {
            seedContext.Rooms.Add(new Room
            {
                Id = roomId,
                CreatedByUserId = creatorId,
                Name = "Room",
                Description = "Room",
                IsPublished = false,
                GraphDefinition = "{}"
            });
            await seedContext.SaveChangesAsync();
        }

        await using var context = new AppDbContext(dbOptions);
        var service = new CreatorRoomService(context, new TriggerGraphValidator());

        var response = await service.PublishAsync(roomId, creatorId, isAdmin: false);
        response.IsPublished.Should().BeTrue();
    }

    [Fact]
    public async Task PublishAsync_ShouldRejectNonOwnerCreator()
    {
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"creator-room-publish-non-owner-{Guid.NewGuid()}")
            .Options;
        var ownerId = Guid.NewGuid();
        var otherCreatorId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        await using (var seedContext = new AppDbContext(dbOptions))
        {
            seedContext.Rooms.Add(new Room
            {
                Id = roomId,
                CreatedByUserId = ownerId,
                Name = "Room",
                Description = "Room",
                IsPublished = false,
                GraphDefinition = "{}"
            });
            await seedContext.SaveChangesAsync();
        }

        await using var context = new AppDbContext(dbOptions);
        var service = new CreatorRoomService(context, new TriggerGraphValidator());

        var act = async () => await service.PublishAsync(roomId, otherCreatorId, isAdmin: false);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
