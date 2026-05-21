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
        await ResetSessionsAsync(cancellationToken);

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
            "Medium escape: combine items, reveal hidden key, and open the final gate.",
            BuildHardRoomDocument(),
            cancellationToken);
        await UpsertStarterRoomAsync(
            creator.Id,
            "Velvet Vault",
            "Hard escape: search an office, build a retrieval gadget, breach the vault, and escape with a final keycard.",
            BuildVaultRoomDocument(),
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ResetSessionsAsync(CancellationToken cancellationToken)
    {
        var sessions = await dbContext.Sessions.ToListAsync(cancellationToken);
        if (sessions.Count == 0)
        {
            return;
        }

        dbContext.Sessions.RemoveRange(sessions);
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
                ThemeId = "clocktower",
                BackgroundColor = "#0b1220",
                Assets =
                [
                    new RoomAssetDto { Id = "clocktower-wall", Kind = "background", VisualKind = "stone-wall", X = 0, Y = 0, Width = 960, Height = 620, ZIndex = 0, Visible = true, Opacity = 1, Color = "#111827" },
                    new RoomAssetDto { Id = "clocktower-floor", Kind = "overlay", VisualKind = "floor-planks", X = 0, Y = 360, Width = 960, Height = 260, ZIndex = 1, Visible = true, Opacity = 1, Color = "#2b2018" },
                    new RoomAssetDto { Id = "clocktower-window", Kind = "sprite", VisualKind = "round-window", X = 52, Y = 38, Width = 230, Height = 230, ZIndex = 2, Visible = true, Opacity = 1, Color = "#dbeafe" },
                    new RoomAssetDto { Id = "clocktower-moonlight", Kind = "overlay", VisualKind = "moonlight", X = 110, Y = 180, Width = 220, Height = 280, ZIndex = 2, Visible = true, Opacity = 0.34, Color = "#93c5fd" },
                    new RoomAssetDto { Id = "clocktower-upper-beam", Kind = "sprite", VisualKind = "beam", X = 0, Y = 72, Width = 960, Height = 32, ZIndex = 3, Visible = true, Opacity = 0.95, Color = "#4a2f23" },
                    new RoomAssetDto { Id = "clocktower-post", Kind = "sprite", VisualKind = "beam", X = 458, Y = 92, Width = 28, Height = 316, ZIndex = 4, Visible = true, Opacity = 0.95, Color = "#4a2f23" },
                    new RoomAssetDto { Id = "clocktower-stair", Kind = "sprite", VisualKind = "stair-silhouette", X = 198, Y = 142, Width = 254, Height = 212, ZIndex = 4, Visible = true, Opacity = 0.9, Color = "#3b2b21" },
                    new RoomAssetDto { Id = "clocktower-shelf", Kind = "sprite", VisualKind = "bookshelf", X = 584, Y = 205, Width = 108, Height = 178, ZIndex = 4, Visible = true, Opacity = 0.92, Color = "#4b2c1d" },
                    new RoomAssetDto { Id = "clocktower-crate", Kind = "sprite", VisualKind = "crate", X = 92, Y = 440, Width = 86, Height = 64, ZIndex = 4, Visible = true, Opacity = 0.95, Color = "#5f4632" },
                    new RoomAssetDto { Id = "clocktower-workbench", Kind = "sprite", VisualKind = "workbench", X = 145, Y = 352, Width = 318, Height = 176, ZIndex = 5, Visible = true, Opacity = 1, Color = "#70492f" },
                    new RoomAssetDto { Id = "clocktower-door-frame", Kind = "sprite", VisualKind = "door-frame", X = 696, Y = 138, Width = 182, Height = 330, ZIndex = 5, Visible = true, Opacity = 1, Color = "#3a261a" },
                    new RoomAssetDto { Id = "clocktower-candle", Kind = "sprite", VisualKind = "candle", X = 626, Y = 320, Width = 38, Height = 74, ZIndex = 6, Visible = true, Opacity = 1, Color = "#fb923c" },
                    new RoomAssetDto { Id = "clocktower-papers", Kind = "sprite", VisualKind = "paper-scatter", X = 472, Y = 496, Width = 112, Height = 44, ZIndex = 6, Visible = true, Opacity = 0.88, Color = "#f8fafc" }
                ],
                Hotspots =
                [
                    new RoomHotspotDto
                    {
                        Id = "final-door",
                        Name = "Final Door",
                        VisualKind = "door",
                        Variant = "locked",
                        X = 714,
                        Y = 150,
                        Width = 146,
                        Height = 300,
                        Color = "#7a4a2a",
                        Locked = true,
                        Available = false,
                        Interactive = false
                    },
                    new RoomHotspotDto
                    {
                        Id = "door-note",
                        Name = "Door Note",
                        VisualKind = "note",
                        Variant = "attached",
                        X = 748,
                        Y = 198,
                        Width = 82,
                        Height = 68,
                        Color = "#fde047"
                    },
                    new RoomHotspotDto
                    {
                        Id = "left-drawer",
                        Name = "Left Drawer",
                        VisualKind = "drawer",
                        Variant = "ajar",
                        X = 214,
                        Y = 410,
                        Width = 108,
                        Height = 58,
                        Color = "#7a5035",
                        Locked = false,
                        Available = true
                    },
                    new RoomHotspotDto
                    {
                        Id = "right-drawer",
                        Name = "Right Drawer",
                        VisualKind = "drawer",
                        Variant = "ajar",
                        X = 332,
                        Y = 410,
                        Width = 108,
                        Height = 58,
                        Color = "#7a5035",
                        Locked = false,
                        Available = true
                    },
                    new RoomHotspotDto
                    {
                        Id = "clocktower-key",
                        Name = "Brass Key",
                        VisualKind = "key",
                        Variant = "hidden",
                        X = 294,
                        Y = 392,
                        Width = 76,
                        Height = 32,
                        Color = "#f6d365",
                        Visible = false,
                        Available = false,
                        Interactive = false
                    },
                    new RoomHotspotDto
                    {
                        Id = "final-lock",
                        Name = "Final Lock",
                        VisualKind = "lock",
                        Variant = "locked",
                        X = 790,
                        Y = 296,
                        Width = 56,
                        Height = 76,
                        Color = "#f4b860",
                        Locked = true,
                        TargetableModes = ["use"],
                        TargetableItemIds = ["brass-key"]
                    }
                ],
                ObjectStates =
                [
                    new RoomObjectStateDto { Id = "door-note", Visible = true, Available = true, Locked = false, Interactive = true },
                    new RoomObjectStateDto { Id = "left-drawer", Visible = true, Available = true, Locked = false, Interactive = true },
                    new RoomObjectStateDto { Id = "right-drawer", Visible = true, Available = true, Locked = false, Interactive = true },
                    new RoomObjectStateDto { Id = "clocktower-key", Visible = false, Available = false, Locked = false, Interactive = false },
                    new RoomObjectStateDto { Id = "final-lock", Visible = true, Available = true, Locked = true, Interactive = true },
                    new RoomObjectStateDto { Id = "final-door", Visible = true, Available = false, Locked = true, Interactive = false }
                ],
                Layers =
                [
                    new RoomLayerDto { Id = "clocktower-moon-glow", Name = "Moon Glow", VisualKind = "moon-glow", ZIndex = 7, Color = "#93c5fd", Opacity = 0.12 },
                    new RoomLayerDto { Id = "clocktower-warm-shadow", Name = "Torch Shadow", VisualKind = "warm-shadow", ZIndex = 8, Color = "#fb923c", Opacity = 0.16 },
                    new RoomLayerDto { Id = "clocktower-vignette", Name = "Vignette", VisualKind = "vignette", ZIndex = 9, Color = "#020617", Opacity = 0.14 },
                    new RoomLayerDto { Id = "clocktower-dust", Name = "Dust", VisualKind = "dust", ZIndex = 10, Color = "#cbd5e1", Opacity = 0.08 }
                ]
            },
            TriggerGraph = new TriggerGraphDefinition
            {
                Metadata = new Dictionary<string, string>
                {
                    ["featured"] = "true",
                    ["difficulty"] = "easy",
                    ["estimatedMinutes"] = "3",
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
                        Config = new Dictionary<string, object?> { ["targetId"] = "door-note" }
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
                            ["clue"] = "A scribble on the door reads: 'The key waits where the wood seam loosens.'"
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-note-unlock-drawer",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "left-drawer",
                            ["locked"] = false,
                            ["available"] = true,
                            ["interactive"] = true
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-note-enable-right-drawer",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "right-drawer",
                            ["locked"] = false,
                            ["available"] = true,
                            ["interactive"] = true
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-note-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "pickup" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-note-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "door-note" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-note-all",
                        Family = "combinator",
                        Type = "allTrue"
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-note-add",
                        Family = "effect",
                        Type = "addInventoryItem",
                        Config = new Dictionary<string, object?>
                        {
                            ["id"] = "door-note",
                            ["label"] = "Yellow Door Note",
                            ["type"] = "clue"
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-note-hide",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "door-note",
                            ["available"] = false,
                            ["visible"] = false,
                            ["interactive"] = false
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-note-msg",
                        Family = "effect",
                        Type = "emitMessage",
                        Config = new Dictionary<string, object?> { ["message"] = "You picked up the yellow note from the door." }
                    },

                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-drawer-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "inspect" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-drawer-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "left-drawer" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-drawer-all",
                        Family = "combinator",
                        Type = "allTrue"
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-drawer-reveal-key",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "clocktower-key",
                            ["visible"] = true,
                            ["available"] = true,
                            ["interactive"] = true
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-drawer-open-drawer",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "left-drawer",
                            ["locked"] = false,
                            ["available"] = false,
                            ["interactive"] = false
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-drawer-msg",
                        Family = "effect",
                        Type = "emitMessage",
                        Config = new Dictionary<string, object?> { ["message"] = "The drawer slides open and a brass key glints inside." }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-right-drawer-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "inspect" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-right-drawer-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "right-drawer" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-right-drawer-all",
                        Family = "combinator",
                        Type = "allTrue"
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-right-drawer-open",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "right-drawer",
                            ["locked"] = false,
                            ["available"] = false,
                            ["interactive"] = false
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-right-drawer-msg",
                        Family = "effect",
                        Type = "emitMessage",
                        Config = new Dictionary<string, object?> { ["message"] = "You open the right drawer. It is empty." }
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
                        Config = new Dictionary<string, object?> { ["targetId"] = "clocktower-key" }
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
                            ["id"] = "brass-key",
                            ["label"] = "Brass Key",
                            ["type"] = "key"
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-key-hide",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "clocktower-key",
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
                        Config = new Dictionary<string, object?> { ["targetId"] = "final-lock" }
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
                            ["objectId"] = "final-lock",
                            ["locked"] = false,
                            ["interactive"] = false,
                            ["available"] = false
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-door-open",
                        Family = "effect",
                        Type = "setStateValue",
                        Config = new Dictionary<string, object?>
                        {
                            ["key"] = "puzzles.clocktower.finalDoorUnlocked",
                            ["value"] = true
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-door-enable-door",
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
                        NodeId = "open-door-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "inspect" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "open-door-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "final-door" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "open-door-unlocked",
                        Family = "condition",
                        Type = "stateValueEquals",
                        Config = new Dictionary<string, object?>
                        {
                            ["key"] = "puzzles.clocktower.finalDoorUnlocked",
                            ["value"] = true
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "open-door-all",
                        Family = "combinator",
                        Type = "allTrue"
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "open-door-disable",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "final-door",
                            ["interactive"] = false,
                            ["available"] = false
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "open-door-complete",
                        Family = "effect",
                        Type = "completeSession",
                        Config = new Dictionary<string, object?> { ["message"] = "The door swings open and you escape the clocktower foyer." }
                    }
                ],
                Edges =
                [
                    new TriggerEdgeDefinition { FromNodeId = "inspect-note-action", ToNodeId = "inspect-note-all" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-note-target", ToNodeId = "inspect-note-all" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-note-all", ToNodeId = "inspect-note-clue" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-note-all", ToNodeId = "inspect-note-unlock-drawer" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-note-all", ToNodeId = "inspect-note-enable-right-drawer" },

                    new TriggerEdgeDefinition { FromNodeId = "pickup-note-action", ToNodeId = "pickup-note-all" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-note-target", ToNodeId = "pickup-note-all" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-note-all", ToNodeId = "pickup-note-add" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-note-all", ToNodeId = "pickup-note-hide" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-note-all", ToNodeId = "pickup-note-msg" },

                    new TriggerEdgeDefinition { FromNodeId = "inspect-drawer-action", ToNodeId = "inspect-drawer-all" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-drawer-target", ToNodeId = "inspect-drawer-all" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-drawer-all", ToNodeId = "inspect-drawer-reveal-key" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-drawer-all", ToNodeId = "inspect-drawer-open-drawer" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-drawer-all", ToNodeId = "inspect-drawer-msg" },

                    new TriggerEdgeDefinition { FromNodeId = "inspect-right-drawer-action", ToNodeId = "inspect-right-drawer-all" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-right-drawer-target", ToNodeId = "inspect-right-drawer-all" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-right-drawer-all", ToNodeId = "inspect-right-drawer-open" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-right-drawer-all", ToNodeId = "inspect-right-drawer-msg" },

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
                    new TriggerEdgeDefinition { FromNodeId = "use-door-all", ToNodeId = "use-door-open" },
                    new TriggerEdgeDefinition { FromNodeId = "use-door-all", ToNodeId = "use-door-enable-door" },

                    new TriggerEdgeDefinition { FromNodeId = "open-door-action", ToNodeId = "open-door-all" },
                    new TriggerEdgeDefinition { FromNodeId = "open-door-target", ToNodeId = "open-door-all" },
                    new TriggerEdgeDefinition { FromNodeId = "open-door-unlocked", ToNodeId = "open-door-all" },
                    new TriggerEdgeDefinition { FromNodeId = "open-door-all", ToNodeId = "open-door-disable" },
                    new TriggerEdgeDefinition { FromNodeId = "open-door-all", ToNodeId = "open-door-complete" }
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
                ThemeId = "crypt",
                BackgroundColor = "#0b1020",
                Assets =
                [
                    new RoomAssetDto { Id = "crypt-wall", Kind = "background", VisualKind = "stone-wall", X = 0, Y = 0, Width = 1024, Height = 640, ZIndex = 0, Visible = true, Opacity = 1, Color = "#0b1020" },
                    new RoomAssetDto { Id = "crypt-floor", Kind = "overlay", VisualKind = "floor-planks", X = 0, Y = 450, Width = 1024, Height = 190, ZIndex = 1, Visible = true, Opacity = 0.72, Color = "#1e293b" },
                    new RoomAssetDto { Id = "crypt-rune-altar", Kind = "sprite", VisualKind = "workbench", X = 92, Y = 110, Width = 250, Height = 180, ZIndex = 2, Visible = true, Opacity = 0.82, Color = "#3b2b4a" },
                    new RoomAssetDto { Id = "crypt-gate-frame", Kind = "sprite", VisualKind = "door-frame", X = 760, Y = 110, Width = 220, Height = 410, ZIndex = 2, Visible = true, Opacity = 1, Color = "#243447" }
                ],
                Hotspots =
                [
                    new RoomHotspotDto
                    {
                        Id = "rune-wall",
                        Name = "Rune Wall",
                        VisualKind = "note",
                        Variant = "etched",
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
                        VisualKind = "switch",
                        Variant = "loose",
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
                        VisualKind = "note",
                        Variant = "flask",
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
                        VisualKind = "drawer",
                        Variant = "sealed",
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
                        VisualKind = "key",
                        Variant = "hidden",
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
                        VisualKind = "door",
                        Variant = "locked",
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
                    new RoomLayerDto { Id = "crypt-mist", Name = "Mist", VisualKind = "dust", ZIndex = 3, Color = "#94a3b8", Opacity = 0.08 },
                    new RoomLayerDto { Id = "crypt-violet-haze", Name = "Violet Haze", VisualKind = "warm-shadow", ZIndex = 4, Color = "#7c3aed", Opacity = 0.07 },
                    new RoomLayerDto { Id = "crypt-vignette", Name = "Vignette", VisualKind = "vignette", ZIndex = 5, Color = "#020617", Opacity = 0.16 }
                ]
            },
            TriggerGraph = new TriggerGraphDefinition
            {
                Metadata = new Dictionary<string, string>
                {
                    ["featured"] = "true",
                    ["difficulty"] = "medium",
                    ["estimatedMinutes"] = "5",
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

    private static EditorDocumentDto BuildVaultRoomDocument()
    {
        return new EditorDocumentDto
        {
            Room = BuildVaultOfficeRoomState(),
            TriggerGraph = new TriggerGraphDefinition
            {
                Metadata = new Dictionary<string, string>
                {
                    ["featured"] = "true",
                    ["difficulty"] = "hard",
                    ["estimatedMinutes"] = "7",
                    ["theme"] = "art deco vault"
                },
                Nodes =
                [
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-dossier-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "inspect" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-dossier-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "entry-dossier" }
                    },
                    new TriggerNodeDefinition { NodeId = "inspect-dossier-all", Family = "combinator", Type = "allTrue" },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-dossier-clue",
                        Family = "effect",
                        Type = "emitClue",
                        Config = new Dictionary<string, object?>
                        {
                            ["clue"] = "Case note: 'The badge magnet finishes the telescoping rod. Retrieve what slipped behind the vent before you try the outer lock.'"
                        }
                    },

                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-left-cabinet-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "inspect" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-left-cabinet-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "left-cabinet-empty" }
                    },
                    new TriggerNodeDefinition { NodeId = "inspect-left-cabinet-all", Family = "combinator", Type = "allTrue" },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-left-cabinet-close",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "left-cabinet-empty",
                            ["available"] = false,
                            ["interactive"] = false
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-left-cabinet-msg",
                        Family = "effect",
                        Type = "emitMessage",
                        Config = new Dictionary<string, object?> { ["message"] = "The left cabinet is empty except for dust and spare hinges." }
                    },

                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-right-cabinet-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "inspect" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-right-cabinet-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "right-cabinet-tool" }
                    },
                    new TriggerNodeDefinition { NodeId = "inspect-right-cabinet-all", Family = "combinator", Type = "allTrue" },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-right-cabinet-reveal-handle",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "telescoping-handle-cache",
                            ["visible"] = true,
                            ["available"] = true,
                            ["interactive"] = true
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-right-cabinet-close",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "right-cabinet-tool",
                            ["available"] = false,
                            ["interactive"] = false
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-right-cabinet-msg",
                        Family = "effect",
                        Type = "emitMessage",
                        Config = new Dictionary<string, object?> { ["message"] = "A telescoping handle clatters loose from the cabinet frame." }
                    },

                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-desk-drawer-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "inspect" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-desk-drawer-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "desk-drawer-empty" }
                    },
                    new TriggerNodeDefinition { NodeId = "inspect-desk-drawer-all", Family = "combinator", Type = "allTrue" },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-desk-drawer-close",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "desk-drawer-empty",
                            ["available"] = false,
                            ["interactive"] = false
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-desk-drawer-msg",
                        Family = "effect",
                        Type = "emitMessage",
                        Config = new Dictionary<string, object?> { ["message"] = "The desk drawer is empty, but the ledge is scratched as if something magnetic was torn away nearby." }
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
                        Config = new Dictionary<string, object?> { ["targetId"] = "telescoping-handle-cache" }
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
                                ["id"] = "telescoping-handle",
                                ["label"] = "Telescoping Handle",
                                ["type"] = "tool",
                                ["quantity"] = 1,
                                ["stack"] = false,
                                ["status"] = "ready",
                                ["combinableWithIds"] = new[] { "badge-magnet" }
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
                            ["objectId"] = "telescoping-handle-cache",
                            ["visible"] = false,
                            ["available"] = false,
                            ["interactive"] = false
                        }
                    },

                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-badge-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "pickup" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-badge-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "door-badge" }
                    },
                    new TriggerNodeDefinition { NodeId = "pickup-badge-all", Family = "combinator", Type = "allTrue" },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-badge-add",
                        Family = "effect",
                        Type = "addInventoryItem",
                        Config = new Dictionary<string, object?>
                        {
                            ["item"] = new Dictionary<string, object?>
                            {
                                ["id"] = "badge-magnet",
                                ["label"] = "Badge Magnet",
                                ["type"] = "gadget",
                                ["quantity"] = 1,
                                ["stack"] = false,
                                ["status"] = "ready",
                                ["combinableWithIds"] = new[] { "telescoping-handle" }
                            }
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-badge-hide",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "door-badge",
                            ["visible"] = false,
                            ["available"] = false,
                            ["interactive"] = false
                        }
                    },

                    new TriggerNodeDefinition
                    {
                        NodeId = "combine-tool-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "inventory.combine" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "combine-tool-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "badge-magnet" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "combine-tool-has-handle",
                        Family = "condition",
                        Type = "inventoryHasItem",
                        Config = new Dictionary<string, object?> { ["itemId"] = "telescoping-handle" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "combine-tool-has-magnet",
                        Family = "condition",
                        Type = "inventoryHasItem",
                        Config = new Dictionary<string, object?> { ["itemId"] = "badge-magnet" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "combine-tool-primary",
                        Family = "condition",
                        Type = "payloadValueEquals",
                        Config = new Dictionary<string, object?> { ["key"] = "primaryItemId", ["value"] = "telescoping-handle" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "combine-tool-secondary",
                        Family = "condition",
                        Type = "payloadValueEquals",
                        Config = new Dictionary<string, object?> { ["key"] = "secondaryItemId", ["value"] = "badge-magnet" }
                    },
                    new TriggerNodeDefinition { NodeId = "combine-tool-all", Family = "combinator", Type = "allTrue" },
                    new TriggerNodeDefinition
                    {
                        NodeId = "combine-tool-remove-handle",
                        Family = "effect",
                        Type = "removeInventoryItem",
                        Config = new Dictionary<string, object?> { ["itemId"] = "telescoping-handle" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "combine-tool-remove-magnet",
                        Family = "effect",
                        Type = "removeInventoryItem",
                        Config = new Dictionary<string, object?> { ["itemId"] = "badge-magnet" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "combine-tool-add-tool",
                        Family = "effect",
                        Type = "addInventoryItem",
                        Config = new Dictionary<string, object?>
                        {
                            ["item"] = new Dictionary<string, object?>
                            {
                                ["id"] = "magnetic-retriever",
                                ["label"] = "Magnetic Retriever",
                                ["type"] = "tool",
                                ["quantity"] = 1,
                                ["stack"] = false,
                                ["status"] = "ready",
                                ["usableTargetIds"] = new[] { "floor-vent" }
                            }
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "combine-tool-msg",
                        Family = "effect",
                        Type = "emitMessage",
                        Config = new Dictionary<string, object?> { ["message"] = "You snap the badge magnet onto the telescoping handle and make a magnetic retriever." }
                    },

                    new TriggerNodeDefinition
                    {
                        NodeId = "use-vent-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "inventory.use" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-vent-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "floor-vent" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-vent-has-tool",
                        Family = "condition",
                        Type = "inventoryHasItem",
                        Config = new Dictionary<string, object?> { ["itemId"] = "magnetic-retriever" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-vent-payload-item",
                        Family = "condition",
                        Type = "payloadValueEquals",
                        Config = new Dictionary<string, object?> { ["key"] = "itemId", ["value"] = "magnetic-retriever" }
                    },
                    new TriggerNodeDefinition { NodeId = "use-vent-all", Family = "combinator", Type = "allTrue" },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-vent-reveal-key",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "office-key-cache",
                            ["visible"] = true,
                            ["available"] = true,
                            ["interactive"] = true
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-vent-disable",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "floor-vent",
                            ["available"] = false,
                            ["interactive"] = false
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-vent-msg",
                        Family = "effect",
                        Type = "emitMessage",
                        Config = new Dictionary<string, object?> { ["message"] = "The retriever snags a brass office key from behind the vent slats." }
                    },

                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-office-key-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "pickup" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-office-key-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "office-key-cache" }
                    },
                    new TriggerNodeDefinition { NodeId = "pickup-office-key-all", Family = "combinator", Type = "allTrue" },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-office-key-add",
                        Family = "effect",
                        Type = "addInventoryItem",
                        Config = new Dictionary<string, object?>
                        {
                            ["item"] = new Dictionary<string, object?>
                            {
                                ["id"] = "office-key",
                                ["label"] = "Office Key",
                                ["type"] = "key",
                                ["quantity"] = 1,
                                ["stack"] = false,
                                ["status"] = "ready",
                                ["usableTargetIds"] = new[] { "outer-lock" }
                            }
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-office-key-hide",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "office-key-cache",
                            ["visible"] = false,
                            ["available"] = false,
                            ["interactive"] = false
                        }
                    },

                    new TriggerNodeDefinition
                    {
                        NodeId = "use-outer-lock-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "inventory.use" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-outer-lock-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "outer-lock" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-outer-lock-has-key",
                        Family = "condition",
                        Type = "inventoryHasItem",
                        Config = new Dictionary<string, object?> { ["itemId"] = "office-key" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-outer-lock-payload-item",
                        Family = "condition",
                        Type = "payloadValueEquals",
                        Config = new Dictionary<string, object?> { ["key"] = "itemId", ["value"] = "office-key" }
                    },
                    new TriggerNodeDefinition { NodeId = "use-outer-lock-all", Family = "combinator", Type = "allTrue" },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-outer-lock-remove-key",
                        Family = "effect",
                        Type = "removeInventoryItem",
                        Config = new Dictionary<string, object?> { ["itemId"] = "office-key" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-outer-lock-unlock",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "outer-lock",
                            ["locked"] = false,
                            ["available"] = false,
                            ["interactive"] = false
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-outer-lock-enable-door",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "outer-door",
                            ["locked"] = false,
                            ["available"] = true,
                            ["interactive"] = true
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-outer-lock-state",
                        Family = "effect",
                        Type = "setStateValue",
                        Config = new Dictionary<string, object?> { ["key"] = "puzzles.velvetVault.outerDoorUnlocked", ["value"] = true }
                    },

                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-outer-door-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "inspect" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-outer-door-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "outer-door" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-outer-door-opened",
                        Family = "condition",
                        Type = "stateValueEquals",
                        Config = new Dictionary<string, object?> { ["key"] = "puzzles.velvetVault.outerDoorUnlocked", ["value"] = true }
                    },
                    new TriggerNodeDefinition { NodeId = "inspect-outer-door-all", Family = "combinator", Type = "allTrue" },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-outer-door-transition",
                        Family = "effect",
                        Type = "transitionRoom",
                        Config = new Dictionary<string, object?>
                        {
                            ["room"] = BuildVaultInnerRoomState(),
                            ["message"] = "You slip through the outer door into the inner vault chamber."
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-outer-door-state",
                        Family = "effect",
                        Type = "setStateValue",
                        Config = new Dictionary<string, object?> { ["key"] = "puzzles.velvetVault.innerVaultEntered", ["value"] = true }
                    },

                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-vault-memo-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "inspect" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-vault-memo-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "vault-memo" }
                    },
                    new TriggerNodeDefinition { NodeId = "inspect-vault-memo-all", Family = "combinator", Type = "allTrue" },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-vault-memo-clue",
                        Family = "effect",
                        Type = "emitClue",
                        Config = new Dictionary<string, object?> { ["clue"] = "Memo: 'The clerk never trusted the open locker. He hid the vault key in the writing desk instead.'" }
                    },

                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-side-locker-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "inspect" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-side-locker-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "side-locker-empty" }
                    },
                    new TriggerNodeDefinition { NodeId = "inspect-side-locker-all", Family = "combinator", Type = "allTrue" },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-side-locker-close",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "side-locker-empty",
                            ["available"] = false,
                            ["interactive"] = false
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-side-locker-msg",
                        Family = "effect",
                        Type = "emitMessage",
                        Config = new Dictionary<string, object?> { ["message"] = "The side locker is empty. Someone cleaned it out in a hurry." }
                    },

                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-clerk-drawer-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "inspect" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-clerk-drawer-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "clerk-drawer" }
                    },
                    new TriggerNodeDefinition { NodeId = "inspect-clerk-drawer-all", Family = "combinator", Type = "allTrue" },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-clerk-drawer-reveal-key",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "vault-key-cache",
                            ["visible"] = true,
                            ["available"] = true,
                            ["interactive"] = true
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-clerk-drawer-close",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "clerk-drawer",
                            ["available"] = false,
                            ["interactive"] = false
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "inspect-clerk-drawer-msg",
                        Family = "effect",
                        Type = "emitMessage",
                        Config = new Dictionary<string, object?> { ["message"] = "A slim vault key rests under the clerk drawer's false bottom." }
                    },

                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-vault-key-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "pickup" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-vault-key-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "vault-key-cache" }
                    },
                    new TriggerNodeDefinition { NodeId = "pickup-vault-key-all", Family = "combinator", Type = "allTrue" },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-vault-key-add",
                        Family = "effect",
                        Type = "addInventoryItem",
                        Config = new Dictionary<string, object?>
                        {
                            ["item"] = new Dictionary<string, object?>
                            {
                                ["id"] = "vault-key",
                                ["label"] = "Vault Key",
                                ["type"] = "key",
                                ["quantity"] = 1,
                                ["stack"] = false,
                                ["status"] = "ready",
                                ["usableTargetIds"] = new[] { "small-vault" }
                            }
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-vault-key-hide",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "vault-key-cache",
                            ["visible"] = false,
                            ["available"] = false,
                            ["interactive"] = false
                        }
                    },

                    new TriggerNodeDefinition
                    {
                        NodeId = "use-small-vault-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "inventory.use" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-small-vault-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "small-vault" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-small-vault-has-key",
                        Family = "condition",
                        Type = "inventoryHasItem",
                        Config = new Dictionary<string, object?> { ["itemId"] = "vault-key" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-small-vault-payload-item",
                        Family = "condition",
                        Type = "payloadValueEquals",
                        Config = new Dictionary<string, object?> { ["key"] = "itemId", ["value"] = "vault-key" }
                    },
                    new TriggerNodeDefinition { NodeId = "use-small-vault-all", Family = "combinator", Type = "allTrue" },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-small-vault-remove-key",
                        Family = "effect",
                        Type = "removeInventoryItem",
                        Config = new Dictionary<string, object?> { ["itemId"] = "vault-key" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-small-vault-open",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "small-vault",
                            ["locked"] = false,
                            ["available"] = false,
                            ["interactive"] = false
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-small-vault-reveal-card",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "exit-keycard-cache",
                            ["visible"] = true,
                            ["available"] = true,
                            ["interactive"] = true
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-small-vault-msg",
                        Family = "effect",
                        Type = "emitMessage",
                        Config = new Dictionary<string, object?> { ["message"] = "The vault clicks open, revealing an ivory keycard in a velvet tray." }
                    },

                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-exit-card-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "pickup" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-exit-card-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "exit-keycard-cache" }
                    },
                    new TriggerNodeDefinition { NodeId = "pickup-exit-card-all", Family = "combinator", Type = "allTrue" },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-exit-card-add",
                        Family = "effect",
                        Type = "addInventoryItem",
                        Config = new Dictionary<string, object?>
                        {
                            ["item"] = new Dictionary<string, object?>
                            {
                                ["id"] = "exit-keycard",
                                ["label"] = "Ivory Keycard",
                                ["type"] = "keycard",
                                ["quantity"] = 1,
                                ["stack"] = false,
                                ["status"] = "ready",
                                ["usableTargetIds"] = new[] { "final-reader" }
                            }
                        }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "pickup-exit-card-hide",
                        Family = "effect",
                        Type = "setObjectState",
                        Config = new Dictionary<string, object?>
                        {
                            ["objectId"] = "exit-keycard-cache",
                            ["visible"] = false,
                            ["available"] = false,
                            ["interactive"] = false
                        }
                    },

                    new TriggerNodeDefinition
                    {
                        NodeId = "use-final-reader-action",
                        Family = "condition",
                        Type = "actionTypeEquals",
                        Config = new Dictionary<string, object?> { ["expectedActionType"] = "inventory.use" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-final-reader-target",
                        Family = "condition",
                        Type = "targetEquals",
                        Config = new Dictionary<string, object?> { ["targetId"] = "final-reader" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-final-reader-has-card",
                        Family = "condition",
                        Type = "inventoryHasItem",
                        Config = new Dictionary<string, object?> { ["itemId"] = "exit-keycard" }
                    },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-final-reader-payload-card",
                        Family = "condition",
                        Type = "payloadValueEquals",
                        Config = new Dictionary<string, object?> { ["key"] = "itemId", ["value"] = "exit-keycard" }
                    },
                    new TriggerNodeDefinition { NodeId = "use-final-reader-all", Family = "combinator", Type = "allTrue" },
                    new TriggerNodeDefinition
                    {
                        NodeId = "use-final-reader-complete",
                        Family = "effect",
                        Type = "completeSession",
                        Config = new Dictionary<string, object?> { ["message"] = "The final reader accepts the keycard and the velvet vault opens to the street. You escaped." }
                    }
                ],
                Edges =
                [
                    new TriggerEdgeDefinition { FromNodeId = "inspect-dossier-action", ToNodeId = "inspect-dossier-all" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-dossier-target", ToNodeId = "inspect-dossier-all" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-dossier-all", ToNodeId = "inspect-dossier-clue" },

                    new TriggerEdgeDefinition { FromNodeId = "inspect-left-cabinet-action", ToNodeId = "inspect-left-cabinet-all" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-left-cabinet-target", ToNodeId = "inspect-left-cabinet-all" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-left-cabinet-all", ToNodeId = "inspect-left-cabinet-close" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-left-cabinet-all", ToNodeId = "inspect-left-cabinet-msg" },

                    new TriggerEdgeDefinition { FromNodeId = "inspect-right-cabinet-action", ToNodeId = "inspect-right-cabinet-all" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-right-cabinet-target", ToNodeId = "inspect-right-cabinet-all" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-right-cabinet-all", ToNodeId = "inspect-right-cabinet-reveal-handle" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-right-cabinet-all", ToNodeId = "inspect-right-cabinet-close" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-right-cabinet-all", ToNodeId = "inspect-right-cabinet-msg" },

                    new TriggerEdgeDefinition { FromNodeId = "inspect-desk-drawer-action", ToNodeId = "inspect-desk-drawer-all" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-desk-drawer-target", ToNodeId = "inspect-desk-drawer-all" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-desk-drawer-all", ToNodeId = "inspect-desk-drawer-close" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-desk-drawer-all", ToNodeId = "inspect-desk-drawer-msg" },

                    new TriggerEdgeDefinition { FromNodeId = "pickup-handle-action", ToNodeId = "pickup-handle-all" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-handle-target", ToNodeId = "pickup-handle-all" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-handle-all", ToNodeId = "pickup-handle-add" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-handle-all", ToNodeId = "pickup-handle-hide" },

                    new TriggerEdgeDefinition { FromNodeId = "pickup-badge-action", ToNodeId = "pickup-badge-all" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-badge-target", ToNodeId = "pickup-badge-all" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-badge-all", ToNodeId = "pickup-badge-add" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-badge-all", ToNodeId = "pickup-badge-hide" },

                    new TriggerEdgeDefinition { FromNodeId = "combine-tool-action", ToNodeId = "combine-tool-all" },
                    new TriggerEdgeDefinition { FromNodeId = "combine-tool-target", ToNodeId = "combine-tool-all" },
                    new TriggerEdgeDefinition { FromNodeId = "combine-tool-has-handle", ToNodeId = "combine-tool-all" },
                    new TriggerEdgeDefinition { FromNodeId = "combine-tool-has-magnet", ToNodeId = "combine-tool-all" },
                    new TriggerEdgeDefinition { FromNodeId = "combine-tool-primary", ToNodeId = "combine-tool-all" },
                    new TriggerEdgeDefinition { FromNodeId = "combine-tool-secondary", ToNodeId = "combine-tool-all" },
                    new TriggerEdgeDefinition { FromNodeId = "combine-tool-all", ToNodeId = "combine-tool-remove-handle" },
                    new TriggerEdgeDefinition { FromNodeId = "combine-tool-all", ToNodeId = "combine-tool-remove-magnet" },
                    new TriggerEdgeDefinition { FromNodeId = "combine-tool-all", ToNodeId = "combine-tool-add-tool" },
                    new TriggerEdgeDefinition { FromNodeId = "combine-tool-all", ToNodeId = "combine-tool-msg" },

                    new TriggerEdgeDefinition { FromNodeId = "use-vent-action", ToNodeId = "use-vent-all" },
                    new TriggerEdgeDefinition { FromNodeId = "use-vent-target", ToNodeId = "use-vent-all" },
                    new TriggerEdgeDefinition { FromNodeId = "use-vent-has-tool", ToNodeId = "use-vent-all" },
                    new TriggerEdgeDefinition { FromNodeId = "use-vent-payload-item", ToNodeId = "use-vent-all" },
                    new TriggerEdgeDefinition { FromNodeId = "use-vent-all", ToNodeId = "use-vent-reveal-key" },
                    new TriggerEdgeDefinition { FromNodeId = "use-vent-all", ToNodeId = "use-vent-disable" },
                    new TriggerEdgeDefinition { FromNodeId = "use-vent-all", ToNodeId = "use-vent-msg" },

                    new TriggerEdgeDefinition { FromNodeId = "pickup-office-key-action", ToNodeId = "pickup-office-key-all" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-office-key-target", ToNodeId = "pickup-office-key-all" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-office-key-all", ToNodeId = "pickup-office-key-add" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-office-key-all", ToNodeId = "pickup-office-key-hide" },

                    new TriggerEdgeDefinition { FromNodeId = "use-outer-lock-action", ToNodeId = "use-outer-lock-all" },
                    new TriggerEdgeDefinition { FromNodeId = "use-outer-lock-target", ToNodeId = "use-outer-lock-all" },
                    new TriggerEdgeDefinition { FromNodeId = "use-outer-lock-has-key", ToNodeId = "use-outer-lock-all" },
                    new TriggerEdgeDefinition { FromNodeId = "use-outer-lock-payload-item", ToNodeId = "use-outer-lock-all" },
                    new TriggerEdgeDefinition { FromNodeId = "use-outer-lock-all", ToNodeId = "use-outer-lock-remove-key" },
                    new TriggerEdgeDefinition { FromNodeId = "use-outer-lock-all", ToNodeId = "use-outer-lock-unlock" },
                    new TriggerEdgeDefinition { FromNodeId = "use-outer-lock-all", ToNodeId = "use-outer-lock-enable-door" },
                    new TriggerEdgeDefinition { FromNodeId = "use-outer-lock-all", ToNodeId = "use-outer-lock-state" },

                    new TriggerEdgeDefinition { FromNodeId = "inspect-outer-door-action", ToNodeId = "inspect-outer-door-all" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-outer-door-target", ToNodeId = "inspect-outer-door-all" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-outer-door-opened", ToNodeId = "inspect-outer-door-all" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-outer-door-all", ToNodeId = "inspect-outer-door-transition" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-outer-door-all", ToNodeId = "inspect-outer-door-state" },

                    new TriggerEdgeDefinition { FromNodeId = "inspect-vault-memo-action", ToNodeId = "inspect-vault-memo-all" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-vault-memo-target", ToNodeId = "inspect-vault-memo-all" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-vault-memo-all", ToNodeId = "inspect-vault-memo-clue" },

                    new TriggerEdgeDefinition { FromNodeId = "inspect-side-locker-action", ToNodeId = "inspect-side-locker-all" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-side-locker-target", ToNodeId = "inspect-side-locker-all" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-side-locker-all", ToNodeId = "inspect-side-locker-close" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-side-locker-all", ToNodeId = "inspect-side-locker-msg" },

                    new TriggerEdgeDefinition { FromNodeId = "inspect-clerk-drawer-action", ToNodeId = "inspect-clerk-drawer-all" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-clerk-drawer-target", ToNodeId = "inspect-clerk-drawer-all" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-clerk-drawer-all", ToNodeId = "inspect-clerk-drawer-reveal-key" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-clerk-drawer-all", ToNodeId = "inspect-clerk-drawer-close" },
                    new TriggerEdgeDefinition { FromNodeId = "inspect-clerk-drawer-all", ToNodeId = "inspect-clerk-drawer-msg" },

                    new TriggerEdgeDefinition { FromNodeId = "pickup-vault-key-action", ToNodeId = "pickup-vault-key-all" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-vault-key-target", ToNodeId = "pickup-vault-key-all" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-vault-key-all", ToNodeId = "pickup-vault-key-add" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-vault-key-all", ToNodeId = "pickup-vault-key-hide" },

                    new TriggerEdgeDefinition { FromNodeId = "use-small-vault-action", ToNodeId = "use-small-vault-all" },
                    new TriggerEdgeDefinition { FromNodeId = "use-small-vault-target", ToNodeId = "use-small-vault-all" },
                    new TriggerEdgeDefinition { FromNodeId = "use-small-vault-has-key", ToNodeId = "use-small-vault-all" },
                    new TriggerEdgeDefinition { FromNodeId = "use-small-vault-payload-item", ToNodeId = "use-small-vault-all" },
                    new TriggerEdgeDefinition { FromNodeId = "use-small-vault-all", ToNodeId = "use-small-vault-remove-key" },
                    new TriggerEdgeDefinition { FromNodeId = "use-small-vault-all", ToNodeId = "use-small-vault-open" },
                    new TriggerEdgeDefinition { FromNodeId = "use-small-vault-all", ToNodeId = "use-small-vault-reveal-card" },
                    new TriggerEdgeDefinition { FromNodeId = "use-small-vault-all", ToNodeId = "use-small-vault-msg" },

                    new TriggerEdgeDefinition { FromNodeId = "pickup-exit-card-action", ToNodeId = "pickup-exit-card-all" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-exit-card-target", ToNodeId = "pickup-exit-card-all" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-exit-card-all", ToNodeId = "pickup-exit-card-add" },
                    new TriggerEdgeDefinition { FromNodeId = "pickup-exit-card-all", ToNodeId = "pickup-exit-card-hide" },

                    new TriggerEdgeDefinition { FromNodeId = "use-final-reader-action", ToNodeId = "use-final-reader-all" },
                    new TriggerEdgeDefinition { FromNodeId = "use-final-reader-target", ToNodeId = "use-final-reader-all" },
                    new TriggerEdgeDefinition { FromNodeId = "use-final-reader-has-card", ToNodeId = "use-final-reader-all" },
                    new TriggerEdgeDefinition { FromNodeId = "use-final-reader-payload-card", ToNodeId = "use-final-reader-all" },
                    new TriggerEdgeDefinition { FromNodeId = "use-final-reader-all", ToNodeId = "use-final-reader-complete" }
                ]
            }
        };
    }

    private static VisualRoomDto BuildVaultOfficeRoomState()
    {
        return new EditorDocumentDto
        {
            Room = new VisualRoomDto
            {
                RoomName = "Velvet Vault Office",
                Width = 1080,
                Height = 660,
                ThemeId = "artdeco",
                BackgroundColor = "#170f21",
                Assets =
                [
                    new RoomAssetDto { Id = "vault-office-wall", Kind = "background", VisualKind = "artdeco-wall", X = 0, Y = 0, Width = 1080, Height = 660, ZIndex = 0, Visible = true, Opacity = 1, Color = "#1a1328" },
                    new RoomAssetDto { Id = "vault-office-floor", Kind = "overlay", VisualKind = "marble-floor", X = 0, Y = 430, Width = 1080, Height = 230, ZIndex = 1, Visible = true, Opacity = 1, Color = "#33253b" },
                    new RoomAssetDto { Id = "vault-office-arch", Kind = "sprite", VisualKind = "deco-arch", X = 720, Y = 70, Width = 250, Height = 390, ZIndex = 2, Visible = true, Opacity = 1, Color = "#6f5b3e" },
                    new RoomAssetDto { Id = "vault-office-desk", Kind = "sprite", VisualKind = "office-desk", X = 170, Y = 350, Width = 320, Height = 170, ZIndex = 3, Visible = true, Opacity = 1, Color = "#5b3b2b" },
                    new RoomAssetDto { Id = "vault-office-cabinets", Kind = "sprite", VisualKind = "filing-cabinet", X = 90, Y = 120, Width = 220, Height = 250, ZIndex = 3, Visible = true, Opacity = 1, Color = "#465161" },
                    new RoomAssetDto { Id = "vault-office-workbench", Kind = "sprite", VisualKind = "workbench", X = 490, Y = 330, Width = 210, Height = 145, ZIndex = 3, Visible = true, Opacity = 1, Color = "#6d4b35" },
                    new RoomAssetDto { Id = "vault-office-door-panel", Kind = "sprite", VisualKind = "reader-panel", X = 805, Y = 235, Width = 64, Height = 118, ZIndex = 4, Visible = true, Opacity = 1, Color = "#e7bf7a" }
                ],
                Hotspots =
                [
                    new RoomHotspotDto { Id = "entry-dossier", Name = "Case Dossier", VisualKind = "note", Variant = "folio", X = 215, Y = 300, Width = 94, Height = 58, Color = "#f8e9b8" },
                    new RoomHotspotDto { Id = "left-cabinet-empty", Name = "Left Cabinet", VisualKind = "drawer", Variant = "steel", X = 120, Y = 188, Width = 82, Height = 108, Color = "#64748b" },
                    new RoomHotspotDto { Id = "right-cabinet-tool", Name = "Right Cabinet", VisualKind = "drawer", Variant = "steel", X = 214, Y = 188, Width = 82, Height = 108, Color = "#64748b" },
                    new RoomHotspotDto { Id = "desk-drawer-empty", Name = "Desk Drawer", VisualKind = "drawer", Variant = "wood", X = 274, Y = 408, Width = 102, Height = 52, Color = "#8a5c3d" },
                    new RoomHotspotDto { Id = "telescoping-handle-cache", Name = "Telescoping Handle", VisualKind = "switch", Variant = "tool", X = 220, Y = 214, Width = 54, Height = 54, Color = "#22d3ee", Visible = false, Available = false, Interactive = false },
                    new RoomHotspotDto { Id = "door-badge", Name = "Security Badge", VisualKind = "note", Variant = "badge", X = 798, Y = 150, Width = 94, Height = 78, Color = "#f59e0b", HitArea = "ellipse" },
                    new RoomHotspotDto { Id = "floor-vent", Name = "Floor Vent", VisualKind = "lock", Variant = "vent", X = 548, Y = 510, Width = 152, Height = 54, Color = "#94a3b8", TargetableModes = ["use"], TargetableItemIds = ["magnetic-retriever"] },
                    new RoomHotspotDto { Id = "office-key-cache", Name = "Office Key", VisualKind = "key", Variant = "hidden", X = 592, Y = 494, Width = 70, Height = 36, Color = "#fcd34d", Visible = false, Available = false, Interactive = false },
                    new RoomHotspotDto { Id = "outer-door", Name = "Outer Security Door", VisualKind = "door", Variant = "locked", X = 754, Y = 102, Width = 184, Height = 330, Color = "#6b4f3a", Locked = true, Available = false, Interactive = false },
                    new RoomHotspotDto { Id = "outer-lock", Name = "Outer Lock", VisualKind = "lock", Variant = "brass", X = 824, Y = 246, Width = 62, Height = 84, Color = "#d4a34f", Locked = true, TargetableModes = ["use"], TargetableItemIds = ["office-key"] }
                ],
                ObjectStates =
                [
                    new RoomObjectStateDto { Id = "entry-dossier", Visible = true, Available = true, Locked = false, Interactive = true },
                    new RoomObjectStateDto { Id = "left-cabinet-empty", Visible = true, Available = true, Locked = false, Interactive = true },
                    new RoomObjectStateDto { Id = "right-cabinet-tool", Visible = true, Available = true, Locked = false, Interactive = true },
                    new RoomObjectStateDto { Id = "desk-drawer-empty", Visible = true, Available = true, Locked = false, Interactive = true },
                    new RoomObjectStateDto { Id = "telescoping-handle-cache", Visible = false, Available = false, Locked = false, Interactive = false },
                    new RoomObjectStateDto { Id = "door-badge", Visible = true, Available = true, Locked = false, Interactive = true },
                    new RoomObjectStateDto { Id = "floor-vent", Visible = true, Available = true, Locked = false, Interactive = true },
                    new RoomObjectStateDto { Id = "office-key-cache", Visible = false, Available = false, Locked = false, Interactive = false },
                    new RoomObjectStateDto { Id = "outer-door", Visible = true, Available = false, Locked = true, Interactive = false },
                    new RoomObjectStateDto { Id = "outer-lock", Visible = true, Available = true, Locked = true, Interactive = true }
                ],
                Layers =
                [
                    new RoomLayerDto { Id = "vault-office-gold-glow", Name = "Gold Glow", VisualKind = "gold-glow", ZIndex = 5, Color = "#f59e0b", Opacity = 0.09 },
                    new RoomLayerDto { Id = "vault-office-shadow", Name = "Shadow", VisualKind = "vignette", ZIndex = 6, Color = "#050816", Opacity = 0.18 },
                    new RoomLayerDto { Id = "vault-office-dust", Name = "Dust", VisualKind = "dust", ZIndex = 7, Color = "#f8fafc", Opacity = 0.05 }
                ]
            }
        }.Room;
    }

    private static VisualRoomDto BuildVaultInnerRoomState()
    {
        return new EditorDocumentDto
        {
            Room = new VisualRoomDto
            {
                RoomName = "Velvet Vault Inner Chamber",
                Width = 1080,
                Height = 660,
                ThemeId = "artdeco",
                BackgroundColor = "#101827",
                Assets =
                [
                    new RoomAssetDto { Id = "vault-inner-wall", Kind = "background", VisualKind = "artdeco-wall", X = 0, Y = 0, Width = 1080, Height = 660, ZIndex = 0, Visible = true, Opacity = 1, Color = "#121a2b" },
                    new RoomAssetDto { Id = "vault-inner-floor", Kind = "overlay", VisualKind = "marble-floor", X = 0, Y = 438, Width = 1080, Height = 222, ZIndex = 1, Visible = true, Opacity = 1, Color = "#273449" },
                    new RoomAssetDto { Id = "vault-inner-safe", Kind = "sprite", VisualKind = "vault-door", X = 430, Y = 145, Width = 220, Height = 220, ZIndex = 2, Visible = true, Opacity = 1, Color = "#94a3b8" },
                    new RoomAssetDto { Id = "vault-inner-desk", Kind = "sprite", VisualKind = "office-desk", X = 160, Y = 360, Width = 250, Height = 146, ZIndex = 2, Visible = true, Opacity = 1, Color = "#5b3b2b" },
                    new RoomAssetDto { Id = "vault-inner-locker", Kind = "sprite", VisualKind = "filing-cabinet", X = 760, Y = 200, Width = 146, Height = 210, ZIndex = 2, Visible = true, Opacity = 1, Color = "#475569" },
                    new RoomAssetDto { Id = "vault-inner-reader", Kind = "sprite", VisualKind = "reader-panel", X = 900, Y = 172, Width = 72, Height = 132, ZIndex = 3, Visible = true, Opacity = 1, Color = "#e7bf7a" }
                ],
                Hotspots =
                [
                    new RoomHotspotDto { Id = "vault-memo", Name = "Vault Memo", VisualKind = "note", Variant = "typed", X = 218, Y = 306, Width = 90, Height = 54, Color = "#f8e9b8" },
                    new RoomHotspotDto { Id = "side-locker-empty", Name = "Side Locker", VisualKind = "drawer", Variant = "steel", X = 782, Y = 242, Width = 96, Height = 132, Color = "#64748b" },
                    new RoomHotspotDto { Id = "clerk-drawer", Name = "Clerk Drawer", VisualKind = "drawer", Variant = "wood", X = 232, Y = 406, Width = 102, Height = 50, Color = "#8a5c3d" },
                    new RoomHotspotDto { Id = "vault-key-cache", Name = "Vault Key", VisualKind = "key", Variant = "hidden", X = 262, Y = 388, Width = 62, Height = 34, Color = "#fde68a", Visible = false, Available = false, Interactive = false },
                    new RoomHotspotDto { Id = "small-vault", Name = "Small Vault", VisualKind = "lock", Variant = "safe", X = 492, Y = 206, Width = 108, Height = 108, Color = "#cbd5e1", Locked = true, TargetableModes = ["use"], TargetableItemIds = ["vault-key"] },
                    new RoomHotspotDto { Id = "exit-keycard-cache", Name = "Ivory Keycard", VisualKind = "note", Variant = "keycard", X = 522, Y = 232, Width = 74, Height = 40, Color = "#f8fafc", Visible = false, Available = false, Interactive = false },
                    new RoomHotspotDto { Id = "final-reader", Name = "Exit Reader", VisualKind = "switch", Variant = "reader", X = 906, Y = 196, Width = 60, Height = 96, Color = "#f59e0b", TargetableModes = ["use"], TargetableItemIds = ["exit-keycard"] }
                ],
                ObjectStates =
                [
                    new RoomObjectStateDto { Id = "vault-memo", Visible = true, Available = true, Locked = false, Interactive = true },
                    new RoomObjectStateDto { Id = "side-locker-empty", Visible = true, Available = true, Locked = false, Interactive = true },
                    new RoomObjectStateDto { Id = "clerk-drawer", Visible = true, Available = true, Locked = false, Interactive = true },
                    new RoomObjectStateDto { Id = "vault-key-cache", Visible = false, Available = false, Locked = false, Interactive = false },
                    new RoomObjectStateDto { Id = "small-vault", Visible = true, Available = true, Locked = true, Interactive = true },
                    new RoomObjectStateDto { Id = "exit-keycard-cache", Visible = false, Available = false, Locked = false, Interactive = false },
                    new RoomObjectStateDto { Id = "final-reader", Visible = true, Available = true, Locked = false, Interactive = true }
                ],
                Layers =
                [
                    new RoomLayerDto { Id = "vault-inner-gold-glow", Name = "Gold Glow", VisualKind = "gold-glow", ZIndex = 4, Color = "#f59e0b", Opacity = 0.08 },
                    new RoomLayerDto { Id = "vault-inner-cool-glow", Name = "Steel Glow", VisualKind = "moon-glow", ZIndex = 5, Color = "#93c5fd", Opacity = 0.06 },
                    new RoomLayerDto { Id = "vault-inner-vignette", Name = "Vignette", VisualKind = "vignette", ZIndex = 6, Color = "#020617", Opacity = 0.18 }
                ]
            }
        }.Room;
    }
}
