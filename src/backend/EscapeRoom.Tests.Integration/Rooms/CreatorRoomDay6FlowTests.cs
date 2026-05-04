using System.Text.Json;
using EscapeRoom.Application.Rooms.Contracts;
using EscapeRoom.Domain.Entities;
using EscapeRoom.Infrastructure.Data;
using EscapeRoom.Infrastructure.Rooms;
using EscapeRoom.TriggerEngine.Validation;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace EscapeRoom.Tests.Integration.Rooms;

public class CreatorRoomDay6FlowTests
{
    [Fact]
    public async Task ValidateSaveAndPlaytestFlow_ShouldIncrementVersionsAndUseLatestSavedDocument()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var creatorId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        await using (var seedContext = new AppDbContext(dbOptions))
        {
            await seedContext.Database.EnsureCreatedAsync();
            seedContext.Rooms.Add(new Room
            {
                Id = roomId,
                CreatedByUserId = creatorId,
                Name = "Test Room",
                Description = "Day 6 flow",
                GraphDefinition = "{}",
                CreatedAtUtc = DateTime.UtcNow
            });

            await seedContext.SaveChangesAsync();
        }

        await using var context = new AppDbContext(dbOptions);
        var service = new CreatorRoomService(context, new TriggerGraphValidator());

        var validDocument = new EditorDocumentDto
        {
            Room = new VisualRoomDto
            {
                RoomName = "Version One",
                Width = 900,
                Height = 600,
                Hotspots = []
            },
            TriggerGraph = new()
            {
                Nodes =
                [
                    new() { NodeId = "cond1", Family = "condition", Type = "actionTypeEquals", Config = new() },
                    new() { NodeId = "effect1", Family = "effect", Type = "emitMessage", Config = new() }
                ],
                Edges =
                [
                    new() { FromNodeId = "cond1", ToNodeId = "effect1" }
                ]
            }
        };

        var validateResponse = await service.ValidateAsync(roomId, validDocument, creatorId, isAdmin: false);
        validateResponse.IsValid.Should().BeTrue();
        validateResponse.Issues.Should().BeEmpty();

        var save1 = await service.SaveAsync(roomId, validDocument, creatorId, isAdmin: false);
        save1.Issues.Should().BeEmpty();
        save1.VersionNumber.Should().Be(1);

        validDocument.Room.RoomName = "Version Two";
        var save2 = await service.SaveAsync(roomId, validDocument, creatorId, isAdmin: false);
        save2.Issues.Should().BeEmpty();
        save2.VersionNumber.Should().Be(2);

        var playtest = await service.CreatePlaytestSessionAsync(roomId, creatorId, isAdmin: false);
        playtest.PlayerJoinPath.Should().StartWith("/player?sessionId=");
        playtest.GmJoinPath.Should().StartWith("/gm?sessionId=");

        var session = await context.Sessions.SingleAsync(x => x.Id == playtest.SessionId);
        using var json = JsonDocument.Parse(session.StateSnapshot);
        var roomName = json.RootElement.GetProperty("room").GetProperty("RoomName").GetString();
        roomName.Should().Be("Version Two");
    }
}
