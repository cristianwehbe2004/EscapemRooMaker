using EscapeRoom.Infrastructure.Triggering;
using FluentAssertions;

namespace EscapeRoom.Tests.Unit.Triggering;

public class StateDiffPayloadBuilderTests
{
    [Fact]
    public void Build_ShouldEmitInventoryPatch_ForInventoryMutation()
    {
        const string updatedState = """
        {
          "room": {
            "roomName": "Lab"
          },
          "inventory": [
            { "id": "inv-key", "label": "Rusty Key", "quantity": 1 }
          ]
        }
        """;

        var (patch, fullStateJson) = StateDiffPayloadBuilder.Build(
            updatedState,
            "inventory.use",
            ["inventory"]);

        patch.Should().NotBeNull();
        patch!.Inventory.Should().NotBeNull();
        patch.Inventory!.Count.Should().Be(1);
        fullStateJson.Should().BeNull();
    }

    [Fact]
    public void Build_ShouldEmitRoomPatch_ForRoomMutation()
    {
        const string updatedState = """
        {
          "room": {
            "roomName": "Lab",
            "hotspots": [
              { "id": "locked-chest", "locked": false, "interactive": true }
            ],
            "objectStates": [
              { "id": "locked-chest", "locked": false, "interactive": true }
            ]
          },
          "inventory": []
        }
        """;

        var (patch, fullStateJson) = StateDiffPayloadBuilder.Build(
            updatedState,
            "inspect",
            ["room.hotspots"]);

        patch.Should().NotBeNull();
        patch!.Room.Should().NotBeNull();
        patch.Room!["hotspots"].Should().NotBeNull();
        patch.Room!["objectStates"].Should().NotBeNull();
        fullStateJson.Should().BeNull();
    }

    [Fact]
    public void Build_ShouldFallbackToFullState_ForUnknownNonMessageMutation()
    {
        const string updatedState = """
        {
          "customGraph": {
            "node": "value"
          }
        }
        """;

        var (patch, fullStateJson) = StateDiffPayloadBuilder.Build(
            updatedState,
            "custom.action",
            ["customGraph"]);

        patch.Should().BeNull();
        fullStateJson.Should().Be(updatedState);
    }

    [Fact]
    public void Build_ShouldMarkRoomPatchAsReplace_ForRoomTransition()
    {
        const string updatedState = """
        {
          "room": {
            "roomName": "Inner Vault",
            "themeId": "artdeco",
            "width": 900,
            "height": 560,
            "backgroundColor": "#111827",
            "assets": [],
            "layers": [],
            "hotspots": [],
            "objectStates": []
          },
          "session": {
            "roomName": "Inner Vault"
          }
        }
        """;

        var (patch, fullStateJson) = StateDiffPayloadBuilder.Build(
            updatedState,
            "inspect",
            ["room.transition", "room", "session"]);

        patch.Should().NotBeNull();
        patch!.Room.Should().NotBeNull();
        patch.Room!["replace"]!.GetValue<bool>().Should().BeTrue();
        patch.Room!["themeId"]!.GetValue<string>().Should().Be("artdeco");
        fullStateJson.Should().BeNull();
    }
}
