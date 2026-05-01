import React from "react";
import { Layer, Rect, Stage, Text } from "react-konva";
import { InventoryInteractionMode } from "../ui/InventoryPanel";
import { InventoryItem, RoomAsset, RoomHotspot, RoomLayer, RoomState } from "../../types/gameState";

type RoomCanvasProps = {
  room: RoomState;
  onInspect: (targetId: string) => void;
  onPickup: (targetId: string) => void;
  selectedInventoryItemId?: string | null;
  selectedInventoryItem?: InventoryItem | null;
  interactionMode?: InventoryInteractionMode;
};

const StageNode = Stage as unknown as React.ComponentType<
  React.PropsWithChildren<{ width: number; height: number }>
>;
const LayerNode = Layer as unknown as React.ComponentType<React.PropsWithChildren<object>>;

const toAlphaColor = (hexOrCss: string, alpha: number): string => {
  if (!hexOrCss.startsWith("#") || (hexOrCss.length !== 7 && hexOrCss.length !== 4)) {
    return hexOrCss;
  }

  const normalized =
    hexOrCss.length === 4
      ? `#${hexOrCss[1]}${hexOrCss[1]}${hexOrCss[2]}${hexOrCss[2]}${hexOrCss[3]}${hexOrCss[3]}`
      : hexOrCss;

  const r = Number.parseInt(normalized.slice(1, 3), 16);
  const g = Number.parseInt(normalized.slice(3, 5), 16);
  const b = Number.parseInt(normalized.slice(5, 7), 16);
  return `rgba(${r}, ${g}, ${b}, ${Math.max(0, Math.min(1, alpha))})`;
};

const isHotspotInteractable = (hotspot: RoomHotspot): boolean => {
  if (!hotspot.visible || !hotspot.available || hotspot.locked) {
    return false;
  }

  return hotspot.interactive;
};

const isTargetableForMode = (
  hotspot: RoomHotspot,
  interactionMode: InventoryInteractionMode,
  selectedInventoryItemId: string | null,
  selectedInventoryItem: InventoryItem | null
): boolean => {
  if (!isHotspotInteractable(hotspot)) {
    return false;
  }

  if (interactionMode === "none") {
    return true;
  }

  if (!selectedInventoryItemId) {
    return false;
  }

  if (selectedInventoryItem?.status !== "ready") {
    return false;
  }

  const modeAllowed =
    !hotspot.targetableModes || hotspot.targetableModes.length === 0
      ? true
      : hotspot.targetableModes.includes(interactionMode);

  if (!modeAllowed) {
    return false;
  }

  if (!hotspot.targetableItemIds || hotspot.targetableItemIds.length === 0) {
    if (interactionMode !== "use" || !selectedInventoryItem?.usableTargetIds || selectedInventoryItem.usableTargetIds.length === 0) {
      return true;
    }

    return selectedInventoryItem.usableTargetIds.includes(hotspot.id);
  }

  return hotspot.targetableItemIds.includes(selectedInventoryItemId);
};

const sortByZIndex = <T extends { zIndex: number }>(entries: T[]): T[] => [...entries].sort((a, b) => a.zIndex - b.zIndex);

const RoomCanvas: React.FC<RoomCanvasProps> = ({
  room,
  onInspect,
  onPickup,
  selectedInventoryItemId = null,
  selectedInventoryItem = null,
  interactionMode = "none",
}) => {
  const sortedAssets = sortByZIndex(room.assets);
  const sortedLayers = sortByZIndex(room.layers);

  return (
    <div className="rounded border border-slate-300 bg-slate-950 p-2">
      <StageNode width={room.width} height={room.height}>
        <LayerNode>
          <Rect x={0} y={0} width={room.width} height={room.height} fill={room.backgroundColor} />
          <Text text={room.roomName} x={12} y={10} fill="#e2e8f0" fontSize={18} />

          {sortedAssets
            .filter((asset) => asset.visible)
            .map((asset: RoomAsset) => (
              <Rect
                key={`asset-${asset.id}`}
                x={asset.x}
                y={asset.y}
                width={asset.width}
                height={asset.height}
                fill={toAlphaColor(asset.color ?? "#334155", asset.opacity)}
                cornerRadius={asset.kind === "background" ? 0 : 6}
              />
            ))}

          {sortedLayers
            .filter((layer) => layer.visible && layer.color)
            .map((layer: RoomLayer) => (
              <Rect
                key={`layer-${layer.id}`}
                x={0}
                y={0}
                width={room.width}
                height={room.height}
                fill={toAlphaColor(layer.color as string, layer.opacity)}
              />
            ))}

          {room.hotspots
            .filter((hotspot) => hotspot.visible)
            .map((hotspot) => {
              const targetable = isTargetableForMode(
                hotspot,
                interactionMode,
                selectedInventoryItemId,
                selectedInventoryItem
              );
              const interactable = isHotspotInteractable(hotspot);
              const isSelectionMode = interactionMode === "use" || interactionMode === "combine";
              const strokeColor =
                isSelectionMode && selectedInventoryItemId
                  ? targetable
                    ? "#22d3ee"
                    : "#475569"
                  : undefined;

              return (
                <React.Fragment key={hotspot.id}>
                  <Rect
                    x={hotspot.x}
                    y={hotspot.y}
                    width={hotspot.width}
                    height={hotspot.height}
                    fill={toAlphaColor(hotspot.color, interactable ? 0.85 : 0.35)}
                    cornerRadius={6}
                    stroke={strokeColor}
                    strokeWidth={strokeColor ? 2 : 0}
                    onClick={() => {
                      if (interactable) {
                        onInspect(hotspot.id);
                      }
                    }}
                    onDblClick={() => {
                      if (interactable) {
                        onPickup(hotspot.id);
                      }
                    }}
                  />
                  <Text text={hotspot.name} x={hotspot.x + 4} y={hotspot.y + 4} fill="#0f172a" fontSize={12} />
                </React.Fragment>
              );
            })}
        </LayerNode>
      </StageNode>
      <p className="mt-2 text-xs text-slate-300">Single click: inspect/use | Double click: pickup</p>
    </div>
  );
};

export default RoomCanvas;
