using EscapeRoom.Application.Triggering.Contracts;

namespace EscapeRoom.Application.Rooms.Contracts;

public class EditorDocumentDto
{
    public VisualRoomDto Room { get; set; } = new();
    public TriggerGraphDefinition TriggerGraph { get; set; } = new();
}

public class VisualRoomDto
{
    public string RoomName { get; set; } = "Escape Room";
    public string? ThemeId { get; set; }
    public int Width { get; set; } = 900;
    public int Height { get; set; } = 600;
    public string BackgroundColor { get; set; } = "#0b1220";
    public List<RoomAssetDto> Assets { get; set; } = [];
    public List<RoomLayerDto> Layers { get; set; } = [];
    public List<RoomHotspotDto> Hotspots { get; set; } = [];
    public List<RoomObjectStateDto> ObjectStates { get; set; } = [];
}

public class RoomAssetDto
{
    public string Id { get; set; } = string.Empty;
    public string Kind { get; set; } = "sprite";
    public string? VisualKind { get; set; }
    public string? Variant { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public int ZIndex { get; set; }
    public bool Visible { get; set; } = true;
    public double Opacity { get; set; } = 1;
    public string? Color { get; set; }
    public string? AssetUrl { get; set; }
    public string? ObjectId { get; set; }
}

public class RoomLayerDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? VisualKind { get; set; }
    public int ZIndex { get; set; }
    public bool Visible { get; set; } = true;
    public double Opacity { get; set; } = 1;
    public string? Color { get; set; }
    public string? AssetId { get; set; }
    public string? ObjectId { get; set; }
}

public class RoomHotspotDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? VisualKind { get; set; }
    public string? Variant { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string Color { get; set; } = "#94a3b8";
    public bool Visible { get; set; } = true;
    public bool Available { get; set; } = true;
    public bool Locked { get; set; }
    public bool Interactive { get; set; } = true;
    public string HitArea { get; set; } = "rect";
    public string? LayerId { get; set; }
    public string? ObjectId { get; set; }
    public List<string>? TargetableItemIds { get; set; }
    public List<string>? TargetableModes { get; set; }
}

public class RoomObjectStateDto
{
    public string Id { get; set; } = string.Empty;
    public bool Visible { get; set; } = true;
    public bool Available { get; set; } = true;
    public bool Locked { get; set; }
    public bool Interactive { get; set; } = true;
}

public class ValidationIssueDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

public class ValidateRoomRequest
{
    public EditorDocumentDto Document { get; set; } = new();
}

public class SaveRoomRequest
{
    public EditorDocumentDto Document { get; set; } = new();
}

public class SaveRoomResponse
{
    public Guid RoomId { get; set; }
    public int VersionNumber { get; set; }
    public DateTime SavedAtUtc { get; set; }
    public IReadOnlyList<ValidationIssueDto> Issues { get; set; } = [];
}

public class ValidateRoomResponse
{
    public bool IsValid { get; set; }
    public IReadOnlyList<ValidationIssueDto> Issues { get; set; } = [];
}

public class CreatePlaytestSessionResponse
{
    public Guid SessionId { get; set; }
    public string PlayerJoinPath { get; set; } = string.Empty;
    public string GmJoinPath { get; set; } = string.Empty;
}
