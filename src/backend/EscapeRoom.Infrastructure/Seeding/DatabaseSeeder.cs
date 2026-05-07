using System.Text.Json;
using EscapeRoom.Application.Rooms.Contracts;
using EscapeRoom.Application.Triggering.Contracts;
using EscapeRoom.Domain.Entities;
using EscapeRoom.Domain.Enums;
using EscapeRoom.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EscapeRoom.Infrastructure.Seeding;

public class DatabaseSeeder(AppDbContext dbContext, IPasswordHasher<User> passwordHasher)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);

        var roleSeed =
            new[]
            {
                new { Name = "Creator", Description = "Can create and publish rooms." },
                new { Name = "Player", Description = "Can join and play sessions." },
                new { Name = "GM", Description = "Can moderate active sessions." },
                new { Name = "Admin", Description = "Can manage the global library and moderation." }
            };

        foreach (var role in roleSeed)
        {
            var exists = await dbContext.Roles.AnyAsync(x => x.Name == role.Name, cancellationToken);
            if (exists)
            {
                continue;
            }

            dbContext.Roles.Add(new Role
            {
                Name = role.Name,
                Description = role.Description,
                IsSystem = true,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        var defaultUsers =
            new[]
            {
                new { Username = "creator1", Email = "creator1@escaperoom.local", Password = "Creator123!", Role = UserRole.Creator },
                new { Username = "player1", Email = "player1@escaperoom.local", Password = "Player123!", Role = UserRole.Player },
                new { Username = "gm1", Email = "gm1@escaperoom.local", Password = "Gm123456!", Role = UserRole.GM },
                new { Username = "admin", Email = "admin@escaperoom.local", Password = "Admin123!", Role = UserRole.Admin }
            };

        foreach (var item in defaultUsers)
        {
            var existing = await dbContext.Users.FirstOrDefaultAsync(x => x.Email == item.Email, cancellationToken);
            if (existing is not null)
            {
                continue;
            }

            var user = new User
            {
                Username = item.Username,
                Email = item.Email,
                Role = item.Role,
                CreatedAtUtc = DateTime.UtcNow
            };

            user.PasswordHash = passwordHasher.HashPassword(user, item.Password);
            dbContext.Users.Add(user);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var creator = await dbContext.Users.FirstAsync(x => x.Role == UserRole.Creator, cancellationToken);
        await UpsertStarterRoomAsync(
            creator.Id,
            "Clocktower Foyer",
            "Easy escape: inspect, pick up a key, and unlock the final door.",
            BuildEasyRoomDocument(),
            cancellationToken);
        await UpsertStarterRoomAsync(
            creator.Id,
            "Crypt of Echoes",
            "Hard escape: combine items, reveal hidden key, and open the final gate.",
            BuildHardRoomDocument(),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertStarterRoomAsync(
        Guid creatorId,
        string name,
        string description,
        EditorDocumentDto document,
        CancellationToken cancellationToken)
    {
        var serialized = JsonSerializer.Serialize(document, JsonOptions);
        var existing = await dbContext.Rooms.FirstOrDefaultAsync(x => x.Name == name, cancellationToken);
        if (existing is null)
        {
            dbContext.Rooms.Add(new Room
            {
                Name = name,
                Description = description,
                CreatedByUserId = creatorId,
                IsPublished = true,
                GraphDefinition = serialized,
                CreatedAtUtc = DateTime.UtcNow
            });
            return;
        }

        existing.Description = description;
        existing.IsPublished = true;
        existing.GraphDefinition = serialized;
    }

    private static EditorDocumentDto BuildEasyRoomDocument()
    {
        return new EditorDocumentDto
        {
            Room = new VisualRoomDto
            {
                RoomName = "Clocktower Foyer",
                Width = 960,
                Height = 620,
                BackgroundColor = "#121826",
                Hotspots =
                [
                    new RoomHotspotDto
                    {
                        Id = "note-panel",
                        Name = "Note Panel",
                        X = 120,
                        Y = 180,
                        Width = 160,
                        Height = 90,
                        Color = "#f59e0b"
                    },
                    new RoomHotspotDto
                    {
                        Id = "key-hook",
                        Name = "Key Hook",
                        X = 360,
                        Y = 180,
                        Width = 120,
                        Height = 90,
                        Color = "#10b981"
                    },
                    new RoomHotspotDto
                    {
                        Id = "final-door",
                        Name = "Final Door",
                        X = 710,
                        Y = 120,
                        Width = 170,
                        Height = 360,
                        Color = "#f97316",
                        Locked = true,
                        TargetableModes = ["use"],
                        TargetableItemIds = ["brass-key"]
                    }
                ],
                ObjectStates =
                [
                    new RoomObjectStateDto { Id = "key-hook", Visible = true, Available = true, Locked = false, Interactive = true },
                    new RoomObjectStateDto { Id = "final-door", Visible = true, Available = true, Locked = true, Interactive = true }
                ],
                Layers =
                [
                    new RoomLayerDto { Id = "fog", Name = "Warm Fog", ZIndex = 1, Color = "#1e293b", Opacity = 0.12 }
                ]
            },
            TriggerGraph = new TriggerGraphDefinition
            {
                Metadata = new Dictionary<string, string>
                {
                    ["featured"] = "true",
                    ["difficulty"] = "easy",
                    ["estimatedMinutes"] = "8",
                    ["theme"] = "clocktower foyer"
                },
                Nodes =
                [
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-note-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "inspect" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-note-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "note-panel" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-note-all",
                        Family = "combinator",
                        Type = "allTrue"
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-note-clue",
                        Family = "effect",
                        Type = "emitClue",
                        Config = new Dictionary<string, object?>
                        {
                            ["clue"] = "A scribble reads: 'The brass key opens the last door.'"
                        }
                    },

                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-key-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "pickup" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-key-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "key-hook" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-key-all",
                        Family = "combinator",
                        Type = "allTrue"
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-key-add",
                        Family = "effect",
                        Type = "addInventoryItem",
                        Config = new Dictionary<string, object?>
                        {
                            ["item"] = new Dictionary<string, object?>
                            {
                                ["id"] = "brass-key",
                                ["label"] = "Brass Key",
                                ["type"] = "key",
                                ["quantity"] = 1,
                                ["stack"] = false,
                                ["status"] = "ready",
                                ["usableTargetIds"] = new[] { "final-door" }
                            }
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-key-hide",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "key-hook",
                            ["available"] = false,
                            ["visible"] = false,
                            ["interactive"] = false
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-key-msg",
                        Family = "effect",
                        Type = "emitMessage",
                        Config = new Dictionary<string, object?> { ["message"] = "You picked up a brass key." }
                    },

                    new TriggerNodeDefinition
                    {
                        NodeId = "use-door-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "inventory.use" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-door-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "final-door" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-door-has-key",
                        Family = "condition",
                        Type = "inventoryHasItem",
                        Config = new Dictionary<string, object?> { ["itemId"] = "brass-key" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-door-payload-item",
                        Family = "condition",
                        Type = "payloadValueEquals",
                        Config = new Dictionary<string, object?>
                        {
                            ["key"] = "itemId",
                            ["value"] = "brass-key"
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-door-all",
                        Family = "combinator",
                        Type = "allTrue"
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-door-remove-key",
                        Family = "effect",
                        Type = "removeInventoryItem",
                        Config = new Dictionary<string, object?> { ["itemId"] = "brass-key" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-door-unlock",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "final-door",
                            ["locked"] = false,
                            ["interactive"] = true,
                            ["available"] = true
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-door-complete",
                        Family = "effect",
                        Type = "completeSession",
                        Config = new Dictionary<string, object?> { ["message"] = "You unlocked the final door and escaped." }
                    }
                ],
                Edges =
                [
                    new TriggerEdgeDefinition { FromNodeId = "inspect-note-action", ToNodeId = "inspect-note-all" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-note-target", ToNodeId = "inspect-note-all" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-note-all", ToNodeId = "inspect-note-clue" },

                    new TriggerEdgeDefinition { FromNodeId = "pickup-key-action", ToNodeId = "pickup-key-all" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-key-target", ToNodeId = "pickup-key-all" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-key-all", ToNodeId = "pickup-key-add" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-key-all", ToNodeId = "pickup-key-hide" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-key-all", ToNodeId = "pickup-key-msg" },

                    new TriggerEdgeDefinition { FromNodeId = "use-door-action", ToNodeId = "use-door-all" },
                    new TriggerEdgeDefinition { FromNodeId = "use-door-target", ToNodeId = "use-door-all" },
                    new TriggerEdgeDefinition { FromNodeId = "use-door-has-key", ToNodeId = "use-door-all" },
                    new TriggerEdgeDefinition { FromNodeId = "use-door-payload-item", ToNodeId = "use-door-all" },
                    new TriggerEdgeDefinition { FromNodeId = "use-door-all", ToNodeId = "use-door-remove-key" },
                    new TriggerEdgeDefinition { FromNodeId = "use-door-all", ToNodeId = "use-door-unlock" },
                    new TriggerEdgeDefinition { FromNodeId = "use-door-all", ToNodeId = "use-door-complete" }
                ]
            }
        };
    }

    private static EditorDocumentDto BuildHardRoomDocument()
    {
        return new EditorDocumentDto
        {
            Room = new VisualRoomDto
            {
                RoomName = "Crypt of Echoes",
                Width = 1024,
                Height = 640,
                BackgroundColor = "#0f172a",
                Hotspots =
                [
                    new RoomHotspotDto
                    {
                        Id = "rune-wall",
                        Name = "Rune Wall",
                        X = 120,
                        Y = 130,
                        Width = 200,
                        Height = 140,
                        Color = "#c084fc"
                    },
                    new RoomHotspotDto
                    {
                        Id = "torch-handle-cache",
                        Name = "Torch Handle",
                        X = 380,
                        Y = 140,
                        Width = 130,
                        Height = 120,
                        Color = "#22c55e"
                    },
                    new RoomHotspotDto
                    {
                        Id = "oil-flask-cache",
                        Name = "Oil Flask",
                        X = 560,
                        Y = 150,
                        Width = 130,
                        Height = 120,
                        Color = "#14b8a6"
                    },
                    new RoomHotspotDto
                    {
                        Id = "shadow-niche",
                        Name = "Shadow Niche",
                        X = 240,
                        Y = 340,
                        Width = 200,
                        Height = 160,
                        Color = "#64748b",
                        Locked = true,
                        TargetableModes = ["use"],
                        TargetableItemIds = ["lit-torch"]
                    },
                    new RoomHotspotDto
                    {
                        Id = "iron-key-cache",
                        Name = "Hidden Iron Key",
                        X = 500,
                        Y = 360,
                        Width = 140,
                        Height = 110,
                        Color = "#facc15",
                        Visible = false,
                        Available = false,
                        Interactive = false
                    },
                    new RoomHotspotDto
                    {
                        Id = "final-gate",
                        Name = "Final Gate",
                        X = 780,
                        Y = 120,
                        Width = 180,
                        Height = 390,
                        Color = "#f97316",
                        Locked = true,
                        TargetableModes = ["use"],
                        TargetableItemIds = ["iron-key"]
                    }
                ],
                ObjectStates =
                [
                    new RoomObjectStateDto { Id = "torch-handle-cache", Visible = true, Available = true, Locked = false, Interactive = true },
                    new RoomObjectStateDto { Id = "oil-flask-cache", Visible = true, Available = true, Locked = false, Interactive = true },
                    new RoomObjectStateDto { Id = "shadow-niche", Visible = true, Available = true, Locked = true, Interactive = true },
                    new RoomObjectStateDto { Id = "iron-key-cache", Visible = false, Available = false, Locked = false, Interactive = false },
                    new RoomObjectStateDto { Id = "final-gate", Visible = true, Available = true, Locked = true, Interactive = true }
                ],
                Layers =
                [
                    new RoomLayerDto { Id = "mist", Name = "Mist", ZIndex = 1, Color = "#94a3b8", Opacity = 0.09 },
                    new RoomLayerDto { Id = "moon", Name = "Moonlight", ZIndex = 2, Color = "#38bdf8", Opacity = 0.05 }
                ]
            },
            TriggerGraph = new TriggerGraphDefinition
            {
                Metadata = new Dictionary<string, string>
                {
                    ["featured"] = "true",
                    ["difficulty"] = "hard",
                    ["estimatedMinutes"] = "10",
                    ["theme"] = "crypt puzzle"
                },
                Nodes =
                [
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-rune-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "inspect" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-rune-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "rune-wall" }
                    },
                    new TriggerNodeDefinition { NodeId = "inspect-rune-all", Family = "combinator", Type = "allTrue" },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-rune-clue",
                        Family = "effect",
                        Type = "emitClue",
                        Config = new Dictionary<string, object?>
                        {
                            ["clue"] = "Etching: 'Join wood and oil, then light the darkness.'"
                        }
                    },

                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-handle-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "pickup" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-handle-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "torch-handle-cache" }
                    },
                    new TriggerNodeDefinition { NodeId = "pickup-handle-all", Family = "combinator", Type = "allTrue" },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-handle-add",
                        Family = "effect",
                        Type = "addInventoryItem",
                        Config = new Dictionary<string, object?>
                        {
                            ["item"] = new Dictionary<string, object?>
                            {
                                ["id"] = "torch-handle",
                                ["label"] = "Torch Handle",
                                ["type"] = "tool",
                                ["quantity"] = 1,
                                ["stack"] = false,
                                ["status"] = "ready",
                                ["combinableWithIds"] = new[] { "oil-flask" }
                            }
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-handle-hide",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "torch-handle-cache",
                            ["available"] = false,
                            ["visible"] = false,
                            ["interactive"] = false
                        }
                    },

                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-oil-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "pickup" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-oil-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "oil-flask-cache" }
                    },
                    new TriggerNodeDefinition { NodeId = "pickup-oil-all", Family = "combinator", Type = "allTrue" },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-oil-add",
                        Family = "effect",
                        Type = "addInventoryItem",
                        Config = new Dictionary<string, object?>
                        {
                            ["item"] = new Dictionary<string, object?>
                            {
                                ["id"] = "oil-flask",
                                ["label"] = "Oil Flask",
                                ["type"] = "liquid",
                                ["quantity"] = 1,
                                ["stack"] = false,
                                ["status"] = "ready",
                                ["combinableWithIds"] = new[] { "torch-handle" }
                            }
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-oil-hide",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "oil-flask-cache",
                            ["available"] = false,
                            ["visible"] = false,
                            ["interactive"] = false
                        }
                    },

                    new TriggerNodeDefinition
                    {
                        NodeId = "combine-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "inventory.combine" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "combine-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "oil-flask" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "combine-has-handle",
                        Family = "condition",
                        Type = "inventoryHasItem",
                        Config = new Dictionary<string, object?> { ["itemId"] = "torch-handle" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "combine-has-oil",
                        Family = "condition",
                        Type = "inventoryHasItem",
                        Config = new Dictionary<string, object?> { ["itemId"] = "oil-flask" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "combine-primary-item",
                        Family = "condition",
                        Type = "payloadValueEquals",
                        Config = new Dictionary<string, object?>
                        {
                            ["key"] = "primaryItemId",
                            ["value"] = "torch-handle"
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "combine-secondary-item",
                        Family = "condition",
                        Type = "payloadValueEquals",
                        Config = new Dictionary<string, object?>
                        {
                            ["key"] = "secondaryItemId",
                            ["value"] = "oil-flask"
                        }
                    },
                    new TriggerNodeDefinition { NodeId = "combine-all", Family = "combinator", Type = "allTrue" },
                    new TriggerNodeDefinition
                    {
                        NodeId = "combine-remove-handle",
                        Family = "effect",
                        Type = "removeInventoryItem",
                        Config = new Dictionary<string, object?> { ["itemId"] = "torch-handle" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "combine-remove-oil",
                        Family = "effect",
                        Type = "removeInventoryItem",
                        Config = new Dictionary<string, object?> { ["itemId"] = "oil-flask" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "combine-add-lit",
                        Family = "effect",
                        Type = "addInventoryItem",
                        Config = new Dictionary<string, object?>
                        {
                            ["item"] = new Dictionary<string, object?>
                            {
                                ["id"] = "lit-torch",
                                ["label"] = "Lit Torch",
                                ["type"] = "tool",
                                ["quantity"] = 1,
                                ["stack"] = false,
                                ["status"] = "ready",
                                ["usableTargetIds"] = new[] { "shadow-niche" }
                            }
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "combine-msg",
                        Family = "effect",
                        Type = "emitMessage",
                        Config = new Dictionary<string, object?> { ["message"] = "You crafted a lit torch." }
                    },

                    new TriggerNodeDefinition
                    {
                        NodeId = "use-niche-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "inventory.use" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-niche-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "shadow-niche" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-niche-has-torch",
                        Family = "condition",
                        Type = "inventoryHasItem",
                        Config = new Dictionary<string, object?> { ["itemId"] = "lit-torch" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-niche-payload-item",
                        Family = "condition",
                        Type = "payloadValueEquals",
                        Config = new Dictionary<string, object?>
                        {
                            ["key"] = "itemId",
                            ["value"] = "lit-torch"
                        }
                    },
                    new TriggerNodeDefinition { NodeId = "use-niche-all", Family = "combinator", Type = "allTrue" },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-niche-reveal-key",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "iron-key-cache",
                            ["visible"] = true,
                            ["available"] = true,
                            ["interactive"] = true
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-niche-unlock",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "shadow-niche",
                            ["locked"] = false,
                            ["available"] = false,
                            ["interactive"] = false
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-niche-clue",
                        Family = "effect",
                        Type = "emitClue",
                        Config = new Dictionary<string, object?> { ["clue"] = "A hidden iron key falls from the niche." }
                    },

                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-iron-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "pickup" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-iron-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "iron-key-cache" }
                    },
                    new TriggerNodeDefinition { NodeId = "pickup-iron-all", Family = "combinator", Type = "allTrue" },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-iron-add",
                        Family = "effect",
                        Type = "addInventoryItem",
                        Config = new Dictionary<string, object?>
                        {
                            ["item"] = new Dictionary<string, object?>
                            {
                                ["id"] = "iron-key",
                                ["label"] = "Iron Key",
                                ["type"] = "key",
                                ["quantity"] = 1,
                                ["stack"] = false,
                                ["status"] = "ready",
                                ["usableTargetIds"] = new[] { "final-gate" }
                            }
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-iron-hide",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "iron-key-cache",
                            ["available"] = false,
                            ["visible"] = false,
                            ["interactive"] = false
                        }
                    },

                    new TriggerNodeDefinition
                    {
                        NodeId = "use-gate-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "inventory.use" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-gate-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "final-gate" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-gate-has-key",
                        Family = "condition",
                        Type = "inventoryHasItem",
                        Config = new Dictionary<string, object?> { ["itemId"] = "iron-key" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-gate-payload-item",
                        Family = "condition",
                        Type = "payloadValueEquals",
                        Config = new Dictionary<string, object?>
                        {
                            ["key"] = "itemId",
                            ["value"] = "iron-key"
                        }
                    },
                    new TriggerNodeDefinition { NodeId = "use-gate-all", Family = "combinator", Type = "allTrue" },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-gate-remove-key",
                        Family = "effect",
                        Type = "removeInventoryItem",
                        Config = new Dictionary<string, object?> { ["itemId"] = "iron-key" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-gate-unlock",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "final-gate",
                            ["locked"] = false,
                            ["available"] = true,
                            ["interactive"] = true
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-gate-complete",
                        Family = "effect",
                        Type = "completeSession",
                        Config = new Dictionary<string, object?> { ["message"] = "The final gate opens. You escaped the crypt." }
                    }
                ],
                Edges =
                [
                    new TriggerEdgeDefinition { FromNodeId = "inspect-rune-action", ToNodeId = "inspect-rune-all" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-rune-target", ToNodeId = "inspect-rune-all" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-rune-all", ToNodeId = "inspect-rune-clue" },

                    new TriggerEdgeDefinition { FromNodeId = "pickup-handle-action", ToNodeId = "pickup-handle-all" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-handle-target", ToNodeId = "pickup-handle-all" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-handle-all", ToNodeId = "pickup-handle-add" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-handle-all", ToNodeId = "pickup-handle-hide" },

                    new TriggerEdgeDefinition { FromNodeId = "pickup-oil-action", ToNodeId = "pickup-oil-all" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-oil-target", ToNodeId = "pickup-oil-all" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-oil-all", ToNodeId = "pickup-oil-add" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-oil-all", ToNodeId = "pickup-oil-hide" },

                    new TriggerEdgeDefinition { FromNodeId = "combine-action", ToNodeId = "combine-all" },
                    new TriggerEdgeDefinition { FromNodeId = "combine-target", ToNodeId = "combine-all" },
                    new TriggerEdgeDefinition { FromNodeId = "combine-has-handle", ToNodeId = "combine-all" },
                    new TriggerEdgeDefinition { FromNodeId = "combine-has-oil", ToNodeId = "combine-all" },
                    new TriggerEdgeDefinition { FromNodeId = "combine-primary-item", ToNodeId = "combine-all" },
                    new TriggerEdgeDefinition { FromNodeId = "combine-secondary-item", ToNodeId = "combine-all" },
                    new TriggerEdgeDefinition { FromNodeId = "combine-all", ToNodeId = "combine-remove-handle" },
                    new TriggerEdgeDefinition { FromNodeId = "combine-all", ToNodeId = "combine-remove-oil" },
                    new TriggerEdgeDefinition { FromNodeId = "combine-all", ToNodeId = "combine-add-lit" },
                    new TriggerEdgeDefinition { FromNodeId = "combine-all", ToNodeId = "combine-msg" },

                    new TriggerEdgeDefinition { FromNodeId = "use-niche-action", ToNodeId = "use-niche-all" },
                    new TriggerEdgeDefinition { FromNodeId = "use-niche-target", ToNodeId = "use-niche-all" },
                    new TriggerEdgeDefinition { FromNodeId = "use-niche-has-torch", ToNodeId = "use-niche-all" },
                    new TriggerEdgeDefinition { FromNodeId = "use-niche-payload-item", ToNodeId = "use-niche-all" },
                    new TriggerEdgeDefinition { FromNodeId = "use-niche-all", ToNodeId = "use-niche-reveal-key" },
                    new TriggerEdgeDefinition { FromNodeId = "use-niche-all", ToNodeId = "use-niche-unlock" },
                    new TriggerEdgeDefinition { FromNodeId = "use-niche-all", ToNodeId = "use-niche-clue" },

                    new TriggerEdgeDefinition { FromNodeId = "pickup-iron-action", ToNodeId = "pickup-iron-all" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-iron-target", ToNodeId = "pickup-iron-all" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-iron-all", ToNodeId = "pickup-iron-add" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-iron-all", ToNodeId = "pickup-iron-hide" },

                    new TriggerEdgeDefinition { FromNodeId = "use-gate-action", ToNodeId = "use-gate-all" },
                    new TriggerEdgeDefinition { FromNodeId = "use-gate-target", ToNodeId = "use-gate-all" },
                    new TriggerEdgeDefinition { FromNodeId = "use-gate-has-key", ToNodeId = "use-gate-all" },
                    new TriggerEdgeDefinition { FromNodeId = "use-gate-payload-item", ToNodeId = "use-gate-all" },
                    new TriggerEdgeDefinition { FromNodeId = "use-gate-all", ToNodeId = "use-gate-remove-key" },
                    new TriggerEdgeDefinition { FromNodeId = "use-gate-all", ToNodeId = "use-gate-unlock" },
                    new TriggerEdgeDefinition { FromNodeId = "use-gate-all", ToNodeId = "use-gate-complete" }
                ]
            }
        };
    }
}
