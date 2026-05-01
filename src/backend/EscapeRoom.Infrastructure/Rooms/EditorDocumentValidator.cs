using EscapeRoom.Application.Rooms.Contracts;
using EscapeRoom.TriggerEngine.Validation;

namespace EscapeRoom.Infrastructure.Rooms;

internal static class EditorDocumentValidator
{
    public static List<ValidationIssueDto> Validate(EditorDocumentDto document, ITriggerGraphValidator graphValidator)
    {
        var issues = new List<ValidationIssueDto>();
        if (document.Room.Width <= 0 || document.Room.Height <= 0)
        {
            issues.Add(new ValidationIssueDto { Code = "room.bounds", Path = "room", Message = "Room width/height must be positive." });
        }

        var hotspotIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (hotspot, index) in document.Room.Hotspots.Select((x, i) => (x, i)))
        {
            var path = $"room.hotspots[{index}]";
            if (string.IsNullOrWhiteSpace(hotspot.Id))
            {
                issues.Add(new ValidationIssueDto { Code = "hotspot.id", Path = path, Message = "Hotspot id is required." });
                continue;
            }

            if (!hotspotIds.Add(hotspot.Id))
            {
                issues.Add(new ValidationIssueDto { Code = "hotspot.duplicate", Path = path, Message = $"Duplicate hotspot id '{hotspot.Id}'." });
            }

            if (hotspot.Width <= 0 || hotspot.Height <= 0)
            {
                issues.Add(new ValidationIssueDto { Code = "hotspot.bounds", Path = path, Message = "Hotspot width/height must be positive." });
            }

            if (hotspot.X < 0 || hotspot.Y < 0 || hotspot.X + hotspot.Width > document.Room.Width || hotspot.Y + hotspot.Height > document.Room.Height)
            {
                issues.Add(new ValidationIssueDto { Code = "hotspot.out_of_bounds", Path = path, Message = "Hotspot must be inside room bounds." });
            }
        }

        var objectIds = new HashSet<string>(
            document.Room.ObjectStates.Where(x => !string.IsNullOrWhiteSpace(x.Id)).Select(x => x.Id),
            StringComparer.OrdinalIgnoreCase);

        foreach (var (asset, index) in document.Room.Assets.Select((x, i) => (x, i)))
        {
            var path = $"room.assets[{index}]";
            if (string.IsNullOrWhiteSpace(asset.Id))
            {
                issues.Add(new ValidationIssueDto { Code = "asset.id", Path = path, Message = "Asset id is required." });
            }

            if (!string.IsNullOrWhiteSpace(asset.ObjectId) && !objectIds.Contains(asset.ObjectId))
            {
                issues.Add(new ValidationIssueDto { Code = "asset.object_ref", Path = path, Message = $"Asset references missing object '{asset.ObjectId}'." });
            }
        }

        foreach (var (hotspot, index) in document.Room.Hotspots.Select((x, i) => (x, i)))
        {
            if (!string.IsNullOrWhiteSpace(hotspot.ObjectId) && !objectIds.Contains(hotspot.ObjectId))
            {
                issues.Add(new ValidationIssueDto
                {
                    Code = "hotspot.object_ref",
                    Path = $"room.hotspots[{index}]",
                    Message = $"Hotspot references missing object '{hotspot.ObjectId}'."
                });
            }
        }

        var graphValidation = graphValidator.Validate(document.TriggerGraph);
        foreach (var error in graphValidation.Errors)
        {
            issues.Add(new ValidationIssueDto { Code = "trigger.graph", Path = "triggerGraph", Message = error });
        }

        foreach (var (node, index) in document.TriggerGraph.Nodes.Select((x, i) => (x, i)))
        {
            if (node.Config.TryGetValue("targetId", out var targetValue) && targetValue is not null)
            {
                var target = targetValue.ToString();
                if (!string.IsNullOrWhiteSpace(target) && !hotspotIds.Contains(target))
                {
                    issues.Add(new ValidationIssueDto
                    {
                        Code = "trigger.target_ref",
                        Path = $"triggerGraph.nodes[{index}]",
                        Message = $"Node '{node.NodeId}' references unknown targetId '{target}'."
                    });
                }
            }
        }

        return issues;
    }
}
