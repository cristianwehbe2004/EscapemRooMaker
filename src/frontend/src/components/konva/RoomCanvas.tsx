import React, { useEffect, useMemo, useRef, useState } from "react";
import { Circle, Ellipse, Group, Image as KonvaImage, Layer, Line, Rect, Stage, Text } from "react-konva";
import { InventoryInteractionMode } from "../ui/InventoryPanel";
import { InventoryItem, RoomAsset, RoomHotspot, RoomLayer, RoomState } from "../../types/gameState";

type RoomCanvasProps = {
  room: RoomState;
  onInspect: (targetId: string) => void;
  onPickup: (targetId: string) => void;
  onHotspotFocus?: (targetId: string) => void;
  selectedInventoryItemId?: string | null;
  selectedInventoryItem?: InventoryItem | null;
  interactionMode?: InventoryInteractionMode;
  disabled?: boolean;
};

type ThemePack = {
  id: "clocktower" | "crypt" | "artdeco" | "default";
  ambientShadow: string;
  objectStroke: string;
  warmLight: string;
  coolLight: string;
};

type HotspotFrame = {
  x: number;
  y: number;
  width: number;
  height: number;
};

type DoorAttachmentConfig = {
  anchorId: string;
  childId: string;
  xRatio: number;
  yRatio: number;
  widthRatio: number;
  heightRatio: number;
  animation?: "unlock-drop";
};

type ActiveAttachmentAnimation = {
  startedAt: number;
  hotspot: RoomHotspot;
  frame: HotspotFrame;
  mode: "unlock-drop";
};

const StageNode = Stage as unknown as React.ComponentType<
  React.PropsWithChildren<{ width: number; height: number; scaleX?: number; scaleY?: number }>
>;
const LayerNode = Layer as unknown as React.ComponentType<React.PropsWithChildren<object>>;
const ImageNode = KonvaImage as unknown as React.ComponentType<Record<string, unknown>>;
const EllipseNode = Ellipse as unknown as React.ComponentType<Record<string, unknown>>;
const GroupNode = Group as unknown as React.ComponentType<React.PropsWithChildren<Record<string, unknown>>>;
const CircleNode = Circle as unknown as React.ComponentType<Record<string, unknown>>;
const LineNode = Line as unknown as React.ComponentType<Record<string, unknown>>;
const LOCK_OPEN_ANIMATION_MS = 650;
const CLOCKTOWER_DOOR_ATTACHMENTS: DoorAttachmentConfig[] = [
  {
    anchorId: "final-door",
    childId: "door-note",
    xRatio: 0.23,
    yRatio: 0.16,
    widthRatio: 0.56,
    heightRatio: 0.23,
  },
  {
    anchorId: "final-door",
    childId: "final-lock",
    xRatio: 0.52,
    yRatio: 0.49,
    widthRatio: 0.38,
    heightRatio: 0.25,
    animation: "unlock-drop",
  },
];

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
  if (!hotspot.visible || !hotspot.available) {
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

    const candidateTargetIds = [hotspot.id, hotspot.objectId].filter((value): value is string => Boolean(value));
    return candidateTargetIds.some((targetId) => selectedInventoryItem.usableTargetIds!.includes(targetId));
  }

  return hotspot.targetableItemIds.includes(selectedInventoryItemId);
};

const sortByZIndex = <T extends { zIndex: number }>(entries: T[]): T[] => [...entries].sort((a, b) => a.zIndex - b.zIndex);
const resolveThemePack = (themeId?: string, roomName?: string): ThemePack => {
  const value = `${themeId ?? ""} ${roomName ?? ""}`.toLowerCase();
  if (value.includes("clocktower")) {
    return {
      id: "clocktower",
      ambientShadow: "rgba(7, 12, 28, 0.3)",
      objectStroke: "#f3d38f",
      warmLight: "rgba(245, 158, 11, 0.32)",
      coolLight: "rgba(96, 165, 250, 0.18)",
    };
  }

  if (value.includes("crypt")) {
    return {
      id: "crypt",
      ambientShadow: "rgba(3, 4, 14, 0.36)",
      objectStroke: "#8b7cf6",
      warmLight: "rgba(251, 146, 60, 0.16)",
      coolLight: "rgba(124, 58, 237, 0.18)",
    };
  }

  if (value.includes("artdeco") || value.includes("vault") || value.includes("velvet")) {
    return {
      id: "artdeco",
      ambientShadow: "rgba(6, 10, 24, 0.34)",
      objectStroke: "#f4c46a",
      warmLight: "rgba(245, 158, 11, 0.22)",
      coolLight: "rgba(148, 163, 184, 0.16)",
    };
  }

  return {
    id: "default",
    ambientShadow: "rgba(2, 6, 23, 0.25)",
    objectStroke: "#cbd5e1",
    warmLight: "rgba(245, 158, 11, 0.18)",
    coolLight: "rgba(96, 165, 250, 0.12)",
  };
};

type HotspotKind = "key" | "door" | "note" | "chest" | "drawer" | "lock" | "switch" | "cabinet" | "panel" | "generic";

const getHotspotSemanticText = (hotspot: RoomHotspot): string =>
  `${hotspot.id} ${hotspot.name} ${hotspot.visualKind ?? ""} ${hotspot.variant ?? ""}`.toLowerCase();

const classifyHotspot = (hotspot: RoomHotspot): HotspotKind => {
  const semanticValue = getHotspotSemanticText(hotspot);
  const explicit = hotspot.visualKind?.toLowerCase();
  if (explicit === "key" || explicit === "door" || explicit === "note" || explicit === "chest" || explicit === "drawer" || explicit === "lock" || explicit === "switch" || explicit === "cabinet" || explicit === "panel") {
    if (explicit === "switch" && (semanticValue.includes("reader") || semanticValue.includes("panel"))) {
      return "panel";
    }
    return explicit;
  }

  const value = semanticValue;
  if (value.includes("key")) return "key";
  if (value.includes("door") || value.includes("gate")) return "door";
  if (value.includes("note") || value.includes("panel") || value.includes("book")) return "note";
  if (value.includes("drawer")) return "drawer";
  if (value.includes("cabinet") || value.includes("locker")) return "cabinet";
  if (value.includes("reader") || value.includes("badge") || value.includes("panel")) return "panel";
  if (value.includes("lock")) return "lock";
  if (value.includes("chest") || value.includes("box")) return "chest";
  if (value.includes("switch") || value.includes("lever")) return "switch";
  return "generic";
};

const getHotspotPrimaryActionLabel = (hotspot: RoomHotspot): string => {
  const kind = classifyHotspot(hotspot);
  if (kind === "drawer") {
    return "Open";
  }

  if (kind === "cabinet") {
    return "Open";
  }

  if (kind === "door" && !hotspot.locked) {
    return "Open";
  }

  if (kind === "key") {
    return "Pick up";
  }

  return "Inspect";
};

const shouldPrimaryActionPickup = (hotspot: RoomHotspot): boolean => {
  const semanticValue = getHotspotSemanticText(hotspot);
  const kind = classifyHotspot(hotspot);
  if (kind === "key") {
    return true;
  }

  return (
    semanticValue.includes("flask") ||
    semanticValue.includes("handle") ||
    semanticValue.includes("cache") ||
    semanticValue.includes("badge") ||
    semanticValue.includes("magnet") ||
    semanticValue.includes("retriever")
  );
};

const shouldRenderHotspot = (hotspot: RoomHotspot, theme: ThemePack): boolean => {
  if (!hotspot.visible) {
    return false;
  }

  const kind = classifyHotspot(hotspot);
  if (theme.id === "clocktower" && hotspot.id === "door-note" && !hotspot.available) {
    return false;
  }

  if ((kind === "note" || shouldPrimaryActionPickup(hotspot)) && !hotspot.available) {
    return false;
  }

  return true;
};

const getDoorAttachmentConfig = (theme: ThemePack, hotspotId: string): DoorAttachmentConfig | undefined => {
  if (theme.id !== "clocktower") {
    return undefined;
  }

  return CLOCKTOWER_DOOR_ATTACHMENTS.find((entry) => entry.childId === hotspotId);
};

const getHotspotFrame = (hotspot: RoomHotspot, hotspotById: Map<string, RoomHotspot>, theme: ThemePack): HotspotFrame => {
  const attachment = getDoorAttachmentConfig(theme, hotspot.id);
  if (!attachment) {
    return {
      x: hotspot.x,
      y: hotspot.y,
      width: hotspot.width,
      height: hotspot.height,
    };
  }

  const anchor = hotspotById.get(attachment.anchorId);
  if (!anchor) {
    return {
      x: hotspot.x,
      y: hotspot.y,
      width: hotspot.width,
      height: hotspot.height,
    };
  }

  return {
    x: anchor.x + anchor.width * attachment.xRatio,
    y: anchor.y + anchor.height * attachment.yRatio,
    width: anchor.width * attachment.widthRatio,
    height: anchor.height * attachment.heightRatio,
  };
};

const renderHotspotObject = (
  hotspot: RoomHotspot,
  active: boolean,
  theme: ThemePack,
  pulse: number,
  unlockAlpha: number,
  frame: HotspotFrame,
  options?: {
    nodeId?: string;
    rotation?: number;
    opacity?: number;
    unlockProgress?: number;
  }
) => {
  const kind = classifyHotspot(hotspot);
  const x = frame.x;
  const y = frame.y;
  const w = frame.width;
  const h = frame.height;
  const glow = active ? 0.86 + pulse * 0.22 : 0.58;
  const lockTint = hotspot.locked ? 0.5 : 1;
  const rotation = options?.rotation ?? 0;
  const opacity = options?.opacity ?? 1;
  const unlockProgress = options?.unlockProgress ?? 0;
  const semanticValue = getHotspotSemanticText(hotspot);

  if (semanticValue.includes("badge")) {
    return (
      <GroupNode id={options?.nodeId} x={x} y={y} rotation={rotation} opacity={opacity}>
        <LineNode points={[w * 0.52, 0, w * 0.52, h * 0.18]} stroke="#e2e8f0" strokeWidth={2} opacity={0.8} />
        <CircleNode x={w * 0.52} y={h * 0.12} radius={Math.max(3, Math.min(w, h) * 0.08)} fill="#f8fafc" />
        <Rect x={w * 0.12} y={h * 0.18} width={w * 0.78} height={h * 0.7} fill="#14324a" stroke="#f4c46a" strokeWidth={2} cornerRadius={8} />
        <Rect x={w * 0.2} y={h * 0.28} width={w * 0.62} height={h * 0.14} fill="#f4c46a" cornerRadius={3} opacity={0.9} />
        <CircleNode x={w * 0.28} y={h * 0.58} radius={Math.max(4, Math.min(w, h) * 0.12)} fill="#f59e0b" />
        <LineNode points={[w * 0.42, h * 0.54, w * 0.74, h * 0.54]} stroke="#cbd5e1" strokeWidth={2} />
        <LineNode points={[w * 0.42, h * 0.68, w * 0.66, h * 0.68]} stroke="#94a3b8" strokeWidth={2} />
      </GroupNode>
    );
  }

  if (semanticValue.includes("vent")) {
    return (
      <GroupNode id={options?.nodeId} x={x} y={y} rotation={rotation} opacity={opacity}>
        <Rect x={0} y={0} width={w} height={h} fill="#5b6574" stroke="#cbd5e1" strokeWidth={2} cornerRadius={5} />
        <Rect x={w * 0.04} y={h * 0.08} width={w * 0.92} height={h * 0.84} fill="rgba(15, 23, 42, 0.28)" cornerRadius={4} />
        {Array.from({ length: 5 }).map((_, index) => (
          <LineNode
            key={`${hotspot.id}-vent-slat-${index}`}
            points={[w * 0.12, h * (0.24 + index * 0.12), w * 0.88, h * (0.14 + index * 0.12)]}
            stroke="#cbd5e1"
            strokeWidth={2}
            opacity={0.82}
          />
        ))}
      </GroupNode>
    );
  }

  if (kind === "key") {
    return (
      <GroupNode id={options?.nodeId} x={x} y={y} rotation={rotation} opacity={opacity}>
        <CircleNode x={w * 0.22} y={h * 0.5} radius={Math.max(8, Math.min(w, h) * 0.2)} stroke="#f8d66d" strokeWidth={5 * glow} opacity={lockTint} />
        <LineNode points={[w * 0.33, h * 0.5, w * 0.9, h * 0.5]} stroke="#f8d66d" strokeWidth={6 * glow} lineCap="round" opacity={lockTint} />
        <LineNode points={[w * 0.72, h * 0.5, w * 0.72, h * 0.7, w * 0.64, h * 0.7]} stroke="#f8d66d" strokeWidth={4 * glow} opacity={lockTint} />
      </GroupNode>
    );
  }

  if (kind === "door") {
    return (
      <GroupNode id={options?.nodeId} x={x} y={y} rotation={rotation} opacity={opacity}>
        <Rect x={0} y={0} width={w} height={h} fill={hotspot.locked ? "#4b2d22" : "#6a4c34"} cornerRadius={4} />
        <Rect x={w * 0.08} y={h * 0.08} width={w * 0.84} height={h * 0.84} stroke="#8d6551" strokeWidth={2} />
        <CircleNode x={w * 0.78} y={h * 0.52} radius={Math.max(3, Math.min(w, h) * 0.04)} fill="#d6b06a" />
        {!hotspot.locked && <Rect x={w * 0.05} y={0} width={w * 0.9} height={h} fill="rgba(253, 224, 71, 0.12)" cornerRadius={4} />}
      </GroupNode>
    );
  }

  if (kind === "note") {
    return (
      <GroupNode id={options?.nodeId} x={x} y={y} rotation={rotation} opacity={opacity}>
        <Rect x={4} y={6} width={w} height={h} fill="rgba(51, 65, 85, 0.26)" cornerRadius={3} />
        <Rect x={0} y={0} width={w} height={h} fill={hotspot.color || "#f4edd2"} stroke="#7c5b17" strokeWidth={2} cornerRadius={3} />
        <CircleNode x={w * 0.5} y={h * 0.12} radius={Math.max(2, w * 0.05)} fill="#7c2d12" />
        <LineNode points={[w * 0.1, h * 0.25, w * 0.85, h * 0.25]} stroke="#8b7d58" strokeWidth={2} />
        <LineNode points={[w * 0.1, h * 0.45, w * 0.78, h * 0.45]} stroke="#8b7d58" strokeWidth={2} />
        <LineNode points={[w * 0.1, h * 0.65, w * 0.82, h * 0.65]} stroke="#8b7d58" strokeWidth={2} />
      </GroupNode>
    );
  }

  if (kind === "drawer") {
    const isWood = semanticValue.includes("wood") || semanticValue.includes("desk") || semanticValue.includes("clerk");
    return (
      <GroupNode id={options?.nodeId} x={x} y={y} rotation={rotation} opacity={opacity}>
        <Rect
          x={0}
          y={0}
          width={w}
          height={h}
          fill={isWood ? "rgba(127, 85, 57, 0.2)" : "rgba(71, 85, 105, 0.16)"}
          stroke={isWood ? "#d6a56d" : "#d2b575"}
          strokeWidth={2}
          cornerRadius={6}
        />
        <Rect
          x={w * 0.08}
          y={h * 0.18}
          width={w * 0.84}
          height={h * 0.64}
          stroke={isWood ? "#f1c27d" : "#cbd5e1"}
          strokeWidth={1.6}
          cornerRadius={4}
          opacity={0.75}
        />
        <LineNode
          points={[w * 0.34, h * 0.5, w * 0.66, h * 0.5]}
          stroke={isWood ? "#f8deb1" : "#e2e8f0"}
          strokeWidth={3}
          lineCap="round"
          opacity={0.86}
        />
      </GroupNode>
    );
  }

  if (kind === "cabinet") {
    return (
      <GroupNode id={options?.nodeId} x={x} y={y} rotation={rotation} opacity={opacity}>
        <Rect x={0} y={0} width={w} height={h} fill="rgba(71, 85, 105, 0.14)" stroke="#cbd5e1" strokeWidth={2} cornerRadius={6} />
        <Rect x={w * 0.12} y={h * 0.1} width={w * 0.76} height={h * 0.8} stroke="#d2b575" strokeWidth={1.8} cornerRadius={4} opacity={0.82} />
        <LineNode points={[w * 0.5, h * 0.1, w * 0.5, h * 0.9]} stroke="#d2b575" strokeWidth={1.6} opacity={0.82} />
        <CircleNode x={w * 0.4} y={h * 0.5} radius={Math.max(3, Math.min(w, h) * 0.05)} fill="#f8e3ae" />
        <CircleNode x={w * 0.6} y={h * 0.5} radius={Math.max(3, Math.min(w, h) * 0.05)} fill="#f8e3ae" />
      </GroupNode>
    );
  }

  if (kind === "lock") {
    const shackleLift = unlockProgress * (h * 0.24);
    const shackleRotation = -unlockProgress * 18;
    const bodyDrop = unlockProgress * (h * 0.16);
    const bodyRotation = unlockProgress * 12;
    return (
      <GroupNode id={options?.nodeId} x={x} y={y} rotation={rotation} opacity={opacity}>
        <Rect x={w * 0.14} y={h * 0.38} width={w * 0.74} height={h * 0.56} fill="rgba(15, 23, 42, 0.22)" cornerRadius={5} />
        <GroupNode x={w * 0.5} y={h * 0.28 - shackleLift} rotation={shackleRotation}>
          <LineNode
            points={[-w * 0.2, h * 0.06, -w * 0.2, -h * 0.12, 0, -h * 0.22, w * 0.2, -h * 0.12, w * 0.2, h * 0.06]}
            stroke={hotspot.locked ? "#f6d365" : "#94a3b8"}
            strokeWidth={4}
            lineCap="round"
            lineJoin="round"
          />
        </GroupNode>
        <GroupNode x={w * 0.5} y={h * 0.6 + bodyDrop} rotation={bodyRotation}>
          <Rect x={-w * 0.35} y={-h * 0.28} width={w * 0.7} height={h * 0.58} fill={hotspot.locked ? "#d4a34f" : "#64748b"} cornerRadius={5} />
        </GroupNode>
      </GroupNode>
    );
  }

  if (kind === "chest") {
    return (
      <GroupNode id={options?.nodeId} x={x} y={y} rotation={rotation} opacity={opacity}>
        <Rect x={0} y={h * 0.24} width={w} height={h * 0.76} fill={hotspot.locked ? "#5b3f2c" : "#7a5636"} cornerRadius={5} />
        <Rect x={0} y={0} width={w} height={h * 0.38} fill={hotspot.locked ? "#704b33" : "#926543"} cornerRadius={5} />
        <Rect x={w * 0.46} y={h * 0.48} width={w * 0.08} height={h * 0.2} fill={hotspot.locked ? "#9c7a3d" : "#e7c57d"} />
      </GroupNode>
    );
  }

  if (kind === "switch") {
    return (
      <GroupNode id={options?.nodeId} x={x} y={y} rotation={rotation} opacity={opacity}>
        <Rect x={0} y={0} width={w} height={h} fill="#374151" cornerRadius={6} />
        <LineNode points={[w * 0.4, h * 0.75, w * 0.6, h * 0.3]} stroke="#e5e7eb" strokeWidth={5} lineCap="round" />
      </GroupNode>
    );
  }

  if (kind === "panel") {
    return (
      <GroupNode id={options?.nodeId} x={x} y={y} rotation={rotation} opacity={opacity}>
        <Rect x={0} y={0} width={w} height={h} fill="#263243" cornerRadius={6} />
        <Rect x={w * 0.14} y={h * 0.14} width={w * 0.72} height={h * 0.72} fill="#111827" stroke="#f4c46a" strokeWidth={2} cornerRadius={4} />
        <CircleNode x={w * 0.28} y={h * 0.32} radius={Math.max(3, Math.min(w, h) * 0.06)} fill="#fb923c" />
        <CircleNode x={w * 0.5} y={h * 0.32} radius={Math.max(3, Math.min(w, h) * 0.06)} fill="#fde68a" />
        <CircleNode x={w * 0.72} y={h * 0.32} radius={Math.max(3, Math.min(w, h) * 0.06)} fill="#60a5fa" />
      </GroupNode>
    );
  }

  return (
    <GroupNode id={options?.nodeId} x={x} y={y} rotation={rotation} opacity={opacity}>
      <Rect x={0} y={0} width={w} height={h} fill={toAlphaColor(hotspot.color, hotspot.locked ? 0.45 : 0.82)} cornerRadius={8} />
      {unlockAlpha > 0 && <Rect x={-3} y={-3} width={w + 6} height={h + 6} stroke={theme.objectStroke} strokeWidth={2} opacity={unlockAlpha} cornerRadius={10} />}
    </GroupNode>
  );
};

const CanvasAsset: React.FC<{ asset: RoomAsset; theme: ThemePack }> = ({ asset, theme }) => {
  const [image, setImage] = useState<HTMLImageElement | null>(null);

  useEffect(() => {
    if (!asset.assetUrl) {
      setImage(null);
      return;
    }

    const nextImage = new window.Image();
    nextImage.crossOrigin = "anonymous";
    nextImage.onload = () => setImage(nextImage);
    nextImage.onerror = () => setImage(null);
    nextImage.src = asset.assetUrl;
  }, [asset.assetUrl]);

  if (image) {
    return (
      <ImageNode
        image={image}
        x={asset.x}
        y={asset.y}
        width={asset.width}
        height={asset.height}
        opacity={asset.opacity}
      />
    );
  }

  const visualKind = asset.visualKind?.toLowerCase();
  if (visualKind === "stone-wall") {
    return (
      <GroupNode x={asset.x} y={asset.y}>
        <Rect x={0} y={0} width={asset.width} height={asset.height} fill="#2f3b4e" />
        {Array.from({ length: 7 }).map((_, row) => (
          <LineNode
            key={`${asset.id}-row-${row}`}
            points={[0, 24 + row * 72, asset.width, 24 + row * 72]}
            stroke="rgba(15, 23, 42, 0.35)"
            strokeWidth={3}
          />
        ))}
        {Array.from({ length: 10 }).map((_, index) => (
          <LineNode
            key={`${asset.id}-joint-${index}`}
            points={[
              40 + (index % 5) * 190,
              18 + Math.floor(index / 5) * 145,
              40 + (index % 5) * 190,
              86 + Math.floor(index / 5) * 145,
            ]}
            stroke="rgba(51, 65, 85, 0.45)"
            strokeWidth={2}
          />
        ))}
      </GroupNode>
    );
  }

  if (visualKind === "floor-planks") {
    return (
      <GroupNode x={asset.x} y={asset.y}>
        <Rect x={0} y={0} width={asset.width} height={asset.height} fill="#2f2218" />
        {Array.from({ length: Math.max(6, Math.floor(asset.width / 86)) }).map((_, index) => (
          <Rect
            key={`${asset.id}-plank-${index}`}
            x={index * (asset.width / Math.max(6, Math.floor(asset.width / 86)))}
            y={0}
            width={asset.width / Math.max(6, Math.floor(asset.width / 86)) - 4}
            height={asset.height}
            fill={index % 2 === 0 ? "#4b3426" : "#3b2a20"}
          />
        ))}
        {Array.from({ length: 5 }).map((_, index) => (
          <LineNode
            key={`${asset.id}-shadow-${index}`}
            points={[0, 20 + index * 42, asset.width, 20 + index * 42]}
            stroke="rgba(15, 23, 42, 0.16)"
            strokeWidth={2}
          />
        ))}
      </GroupNode>
    );
  }

  if (visualKind === "round-window") {
    return (
      <GroupNode x={asset.x} y={asset.y}>
        <CircleNode x={asset.width / 2} y={asset.height / 2} radius={Math.min(asset.width, asset.height) / 2} fill="#1e293b" />
        <CircleNode x={asset.width / 2} y={asset.height / 2} radius={Math.min(asset.width, asset.height) / 2.55} fill="#dbeafe" opacity={0.95} />
        {Array.from({ length: 10 }).map((_, index) => {
          const angle = (Math.PI * index) / 10;
          return (
            <LineNode
              key={`${asset.id}-spoke-${index}`}
              points={[
                asset.width / 2,
                asset.height / 2,
                asset.width / 2 + Math.cos(angle) * (asset.width * 0.45),
                asset.height / 2 + Math.sin(angle) * (asset.height * 0.45),
              ]}
              stroke="#334155"
              strokeWidth={3}
            />
          );
        })}
      </GroupNode>
    );
  }

  if (visualKind === "workbench") {
    return (
      <GroupNode x={asset.x} y={asset.y}>
        <Rect x={0} y={0} width={asset.width} height={asset.height * 0.22} fill="#7c5336" cornerRadius={6} />
        <Rect x={asset.width * 0.06} y={asset.height * 0.2} width={asset.width * 0.18} height={asset.height * 0.78} fill="#5d3b28" />
        <Rect x={asset.width * 0.76} y={asset.height * 0.2} width={asset.width * 0.18} height={asset.height * 0.78} fill="#5d3b28" />
        <Rect x={asset.width * 0.28} y={asset.height * 0.26} width={asset.width * 0.44} height={asset.height * 0.6} fill="#6b442d" cornerRadius={4} />
      </GroupNode>
    );
  }

  if (visualKind === "bookshelf") {
    return (
      <GroupNode x={asset.x} y={asset.y}>
        <Rect x={0} y={0} width={asset.width} height={asset.height} fill="#473120" cornerRadius={4} />
        {[0.2, 0.44, 0.68].map((slot) => (
          <LineNode key={`${asset.id}-${slot}`} points={[0, asset.height * slot, asset.width, asset.height * slot]} stroke="#8a5f3f" strokeWidth={3} />
        ))}
        {Array.from({ length: 7 }).map((_, index) => (
          <Rect
            key={`${asset.id}-book-${index}`}
            x={12 + (index % 3) * ((asset.width - 28) / 3)}
            y={18 + Math.floor(index / 3) * 34}
            width={16}
            height={24 + (index % 2) * 4}
            fill={index % 2 === 0 ? "#7f1d1d" : "#1d4ed8"}
          />
        ))}
      </GroupNode>
    );
  }

  if (visualKind === "crate") {
    return (
      <GroupNode x={asset.x} y={asset.y}>
        <Rect x={0} y={0} width={asset.width} height={asset.height} fill="#5b3b29" cornerRadius={4} />
        <LineNode points={[8, 10, asset.width - 8, 10]} stroke="#8b5e3c" strokeWidth={3} />
        <LineNode points={[8, asset.height - 10, asset.width - 8, asset.height - 10]} stroke="#8b5e3c" strokeWidth={3} />
      </GroupNode>
    );
  }

  if (visualKind === "beam") {
    return <Rect x={asset.x} y={asset.y} width={asset.width} height={asset.height} fill="#3f2b1f" cornerRadius={2} opacity={asset.opacity} />;
  }

  if (visualKind === "door-frame") {
    return (
      <GroupNode x={asset.x} y={asset.y}>
        <Rect x={0} y={0} width={asset.width} height={asset.height} fill="#24170f" cornerRadius={6} />
        <Rect x={asset.width * 0.1} y={asset.height * 0.06} width={asset.width * 0.8} height={asset.height * 0.9} fill="#3b2418" cornerRadius={5} />
        <Rect x={asset.width * 0.16} y={asset.height * 0.14} width={asset.width * 0.68} height={asset.height * 0.74} stroke="#5c3a28" strokeWidth={6} />
      </GroupNode>
    );
  }

  if (visualKind === "artdeco-wall") {
    return (
      <GroupNode x={asset.x} y={asset.y}>
        <Rect x={0} y={0} width={asset.width} height={asset.height} fill="#1c1630" />
        {Array.from({ length: 7 }).map((_, index) => (
          <LineNode
            key={`${asset.id}-stripe-${index}`}
            points={[90 + index * 140, 0, 40 + index * 140, asset.height]}
            stroke="rgba(244, 196, 106, 0.18)"
            strokeWidth={8}
          />
        ))}
        <Rect x={0} y={0} width={asset.width} height={32} fill="rgba(244, 196, 106, 0.12)" />
      </GroupNode>
    );
  }

  if (visualKind === "marble-floor") {
    return (
      <GroupNode x={asset.x} y={asset.y}>
        <Rect x={0} y={0} width={asset.width} height={asset.height} fill="#2a2338" />
        {Array.from({ length: 9 }).map((_, index) => (
          <LineNode
            key={`${asset.id}-vein-${index}`}
            points={[index * 120, 0, index * 120 + 80, asset.height]}
            stroke="rgba(226, 232, 240, 0.12)"
            strokeWidth={2}
          />
        ))}
      </GroupNode>
    );
  }

  if (visualKind === "deco-arch") {
    return (
      <GroupNode x={asset.x} y={asset.y}>
        <Rect x={0} y={asset.height * 0.08} width={asset.width} height={asset.height * 0.92} fill="#221627" cornerRadius={8} />
        <Rect x={asset.width * 0.08} y={asset.height * 0.16} width={asset.width * 0.84} height={asset.height * 0.76} stroke="#d2b575" strokeWidth={6} cornerRadius={8} />
        <LineNode points={[asset.width * 0.14, asset.height * 0.26, asset.width * 0.5, 0, asset.width * 0.86, asset.height * 0.26]} stroke="#d2b575" strokeWidth={5} lineJoin="round" />
      </GroupNode>
    );
  }

  if (visualKind === "office-desk") {
    return (
      <GroupNode x={asset.x} y={asset.y}>
        <Rect x={0} y={0} width={asset.width} height={asset.height * 0.24} fill="#70492f" cornerRadius={7} />
        <Rect x={asset.width * 0.06} y={asset.height * 0.2} width={asset.width * 0.22} height={asset.height * 0.78} fill="#503122" cornerRadius={4} />
        <Rect x={asset.width * 0.72} y={asset.height * 0.2} width={asset.width * 0.22} height={asset.height * 0.78} fill="#503122" cornerRadius={4} />
        <Rect x={asset.width * 0.34} y={asset.height * 0.26} width={asset.width * 0.32} height={asset.height * 0.58} fill="#5f3a28" cornerRadius={4} />
      </GroupNode>
    );
  }

  if (visualKind === "filing-cabinet") {
    return (
      <GroupNode x={asset.x} y={asset.y}>
        <Rect x={0} y={0} width={asset.width} height={asset.height} fill="#536275" cornerRadius={6} />
        {[0.08, 0.36, 0.64].map((yRatio) => (
          <GroupNode key={`${asset.id}-${yRatio}`} x={0} y={asset.height * yRatio}>
            <Rect x={asset.width * 0.08} y={0} width={asset.width * 0.84} height={asset.height * 0.22} stroke="#d2b575" strokeWidth={2} cornerRadius={4} />
            <Rect x={asset.width * 0.36} y={asset.height * 0.08} width={asset.width * 0.28} height={asset.height * 0.04} fill="#d2b575" cornerRadius={2} />
          </GroupNode>
        ))}
      </GroupNode>
    );
  }

  if (visualKind === "reader-panel") {
    return (
      <GroupNode x={asset.x} y={asset.y}>
        <Rect x={0} y={0} width={asset.width} height={asset.height} fill="#1f2937" cornerRadius={6} />
        <Rect x={asset.width * 0.14} y={asset.height * 0.12} width={asset.width * 0.72} height={asset.height * 0.76} fill="#0f172a" stroke="#f4c46a" strokeWidth={2} cornerRadius={4} />
        <CircleNode x={asset.width * 0.3} y={asset.height * 0.24} radius={asset.width * 0.08} fill="#fb923c" />
        <CircleNode x={asset.width * 0.5} y={asset.height * 0.24} radius={asset.width * 0.08} fill="#fde68a" />
        <CircleNode x={asset.width * 0.7} y={asset.height * 0.24} radius={asset.width * 0.08} fill="#60a5fa" />
      </GroupNode>
    );
  }

  if (visualKind === "vault-door") {
    return (
      <GroupNode x={asset.x} y={asset.y}>
        <CircleNode x={asset.width / 2} y={asset.height / 2} radius={Math.min(asset.width, asset.height) * 0.5} fill="#475569" />
        <CircleNode x={asset.width / 2} y={asset.height / 2} radius={Math.min(asset.width, asset.height) * 0.38} stroke="#d2b575" strokeWidth={8} />
        <LineNode points={[asset.width * 0.2, asset.height * 0.5, asset.width * 0.8, asset.height * 0.5]} stroke="#d2b575" strokeWidth={6} />
        <LineNode points={[asset.width * 0.5, asset.height * 0.2, asset.width * 0.5, asset.height * 0.8]} stroke="#d2b575" strokeWidth={6} />
        <CircleNode x={asset.width / 2} y={asset.height / 2} radius={asset.width * 0.08} fill="#e5c07b" />
      </GroupNode>
    );
  }

  if (visualKind === "stair-silhouette") {
    return (
      <GroupNode x={asset.x} y={asset.y}>
        {Array.from({ length: 8 }).map((_, index) => (
          <Rect
            key={`${asset.id}-step-${index}`}
            x={index * (asset.width / 10)}
            y={asset.height - (index + 1) * (asset.height / 10)}
            width={asset.width * 0.18}
            height={asset.height * 0.08}
            fill="#433225"
          />
        ))}
        <LineNode points={[0, asset.height, asset.width * 0.88, 0]} stroke="#2d1f16" strokeWidth={8} />
      </GroupNode>
    );
  }

  if (visualKind === "candle") {
    return (
      <GroupNode x={asset.x} y={asset.y}>
        <Rect x={asset.width * 0.42} y={asset.height * 0.28} width={asset.width * 0.16} height={asset.height * 0.56} fill="#f8fafc" cornerRadius={2} />
        <CircleNode x={asset.width * 0.5} y={asset.height * 0.2} radius={asset.width * 0.18} fill="#fb923c" opacity={0.82} />
        <CircleNode x={asset.width * 0.5} y={asset.height * 0.1} radius={asset.width * 0.1} fill="#fde68a" opacity={0.95} />
      </GroupNode>
    );
  }

  if (visualKind === "moonlight") {
    return <Rect x={asset.x} y={asset.y} width={asset.width} height={asset.height} fill={theme.coolLight} opacity={asset.opacity} />;
  }

  if (visualKind === "paper-scatter") {
    return (
      <GroupNode x={asset.x} y={asset.y}>
        <Rect x={0} y={0} width={asset.width * 0.32} height={asset.height * 0.24} fill="#f3f4f6" rotation={-8} />
        <Rect x={asset.width * 0.28} y={asset.height * 0.16} width={asset.width * 0.3} height={asset.height * 0.22} fill="#e5e7eb" rotation={12} />
        <Rect x={asset.width * 0.56} y={asset.height * 0.04} width={asset.width * 0.26} height={asset.height * 0.2} fill="#f8fafc" rotation={-14} />
      </GroupNode>
    );
  }

  return (
    <Rect
      x={asset.x}
      y={asset.y}
      width={asset.width}
      height={asset.height}
      fill={toAlphaColor(asset.color ?? "#334155", asset.opacity)}
      cornerRadius={asset.kind === "background" ? 0 : 6}
    />
  );
};

const CanvasLayerVisual: React.FC<{ layer: RoomLayer; room: RoomState; theme: ThemePack }> = ({ layer, room, theme }) => {
  const visualKind = layer.visualKind?.toLowerCase();
  if (visualKind === "vignette") {
    return <Rect x={0} y={0} width={room.width} height={room.height} fill={theme.ambientShadow} opacity={layer.opacity} />;
  }

  if (visualKind === "dust") {
    return (
      <GroupNode>
        {Array.from({ length: 24 }).map((_, index) => (
          <CircleNode
            key={`${layer.id}-dust-${index}`}
            x={40 + (index * 37) % room.width}
            y={60 + (index * 53) % room.height}
            radius={1 + (index % 3)}
            fill="#cbd5e1"
            opacity={layer.opacity * (0.4 + (index % 5) * 0.1)}
          />
        ))}
      </GroupNode>
    );
  }

  if (visualKind === "moon-glow") {
    return <Rect x={0} y={0} width={room.width} height={room.height} fill={theme.coolLight} opacity={layer.opacity} />;
  }

  if (visualKind === "warm-shadow") {
    return <Rect x={0} y={0} width={room.width} height={room.height} fill={theme.warmLight} opacity={layer.opacity} />;
  }

  return (
    <Rect
      x={0}
      y={0}
      width={room.width}
      height={room.height}
      fill={toAlphaColor(layer.color ?? "#0f172a", layer.opacity)}
    />
  );
};

const RoomCanvas: React.FC<RoomCanvasProps> = ({
  room,
  onInspect,
  onPickup,
  selectedInventoryItemId = null,
  selectedInventoryItem = null,
  interactionMode = "none",
  onHotspotFocus,
  disabled = false,
}) => {
  const sortedAssets = sortByZIndex(room.assets);
  const sortedLayers = sortByZIndex(room.layers);
  const containerRef = useRef<HTMLDivElement | null>(null);
  const [containerWidth, setContainerWidth] = useState(room.width);
  const [hoveredHotspotId, setHoveredHotspotId] = useState<string | null>(null);
  const [animTick, setAnimTick] = useState(0);
  const [unlockFlashes, setUnlockFlashes] = useState<Record<string, number>>({});
  const [pickupBursts, setPickupBursts] = useState<Array<{ id: string; x: number; y: number; createdAt: number }>>([]);
  const [attachmentAnimations, setAttachmentAnimations] = useState<Record<string, ActiveAttachmentAnimation>>({});
  const prevHotspotsRef = useRef<Map<string, RoomHotspot>>(new Map());
  const inspectTimersRef = useRef<Record<string, number>>({});
  const theme = useMemo(() => resolveThemePack(room.themeId, room.roomName), [room.roomName, room.themeId]);
  const scale = useMemo(() => Math.min(1, Math.max(0.25, containerWidth / room.width)), [containerWidth, room.width]);
  const hotspotById = useMemo(() => new Map(room.hotspots.map((hotspot) => [hotspot.id, hotspot])), [room.hotspots]);
  const visibleHotspots = useMemo(
    () => room.hotspots.filter((hotspot) => shouldRenderHotspot(hotspot, theme)),
    [room.hotspots, theme]
  );
  const attachedHotspotIds = useMemo(() => {
    if (theme.id !== "clocktower") {
      return new Set<string>();
    }

    const ids = CLOCKTOWER_DOOR_ATTACHMENTS
      .filter((attachment) => hotspotById.has(attachment.anchorId) && hotspotById.has(attachment.childId))
      .map((attachment) => attachment.childId);
    return new Set(ids);
  }, [hotspotById, theme.id]);
  const baseHotspots = useMemo(
    () => visibleHotspots.filter((hotspot) => !attachedHotspotIds.has(hotspot.id)),
    [attachedHotspotIds, visibleHotspots]
  );
  const overlayHotspots = useMemo(
    () => visibleHotspots.filter((hotspot) => attachedHotspotIds.has(hotspot.id)),
    [attachedHotspotIds, visibleHotspots]
  );

  useEffect(() => {
    const element = containerRef.current;
    if (!element) {
      return;
    }

    const updateWidth = () => setContainerWidth(element.clientWidth || room.width);
    updateWidth();

    if (typeof ResizeObserver === "undefined") {
      window.addEventListener("resize", updateWidth);
      return () => window.removeEventListener("resize", updateWidth);
    }

    const observer = new ResizeObserver(updateWidth);
    observer.observe(element);
    return () => observer.disconnect();
  }, [room.width]);

  useEffect(() => {
    const id = window.setInterval(() => setAnimTick((current) => current + 1), 60);
    return () => window.clearInterval(id);
  }, []);

  useEffect(() => {
    const previous = prevHotspotsRef.current;
    const next = new Map<string, RoomHotspot>();
    const now = Date.now();

    for (const hotspot of room.hotspots) {
      const frame = getHotspotFrame(hotspot, hotspotById, theme);
      const prev = previous.get(hotspot.id);
      if (prev) {
        if (prev.locked && !hotspot.locked) {
          setUnlockFlashes((current) => ({ ...current, [hotspot.id]: now }));
          const attachment = getDoorAttachmentConfig(theme, hotspot.id);
          if (attachment?.animation === "unlock-drop") {
            setAttachmentAnimations((current) => ({
              ...current,
              [hotspot.id]: {
                startedAt: now,
                hotspot: { ...hotspot },
                frame,
                mode: "unlock-drop",
              },
            }));
          }
        }

        if ((prev.visible && !hotspot.visible) || (prev.available && !hotspot.available && !shouldRenderHotspot(hotspot, theme))) {
          setPickupBursts((current) => [
            ...current,
            { id: `${hotspot.id}-${now}`, x: frame.x + frame.width / 2, y: frame.y + frame.height / 2, createdAt: now },
          ]);
        }
      }

      next.set(hotspot.id, { ...hotspot });
    }

    prevHotspotsRef.current = next;
  }, [hotspotById, room.hotspots, theme]);

  useEffect(() => {
    return () => {
      Object.values(inspectTimersRef.current).forEach((timerId) => window.clearTimeout(timerId));
      inspectTimersRef.current = {};
    };
  }, []);

  useEffect(() => {
    const now = Date.now();
    setUnlockFlashes((current) =>
      Object.fromEntries(Object.entries(current).filter(([, startedAt]) => now - startedAt < 900))
    );
    setPickupBursts((current) => current.filter((burst) => now - burst.createdAt < 800));
    setAttachmentAnimations((current) =>
      Object.fromEntries(Object.entries(current).filter(([, animation]) => now - animation.startedAt < LOCK_OPEN_ANIMATION_MS))
    );
  }, [animTick]);

  const renderHotspotEntry = (hotspot: RoomHotspot): React.ReactNode => {
    const frame = getHotspotFrame(hotspot, hotspotById, theme);
    const shouldSuppressStaticRender =
      hotspot.id === "final-lock" &&
      theme.id === "clocktower" &&
      (!hotspot.locked || Boolean(attachmentAnimations[hotspot.id]));
    if (shouldSuppressStaticRender) {
      return null;
    }

    const targetable = isTargetableForMode(
      hotspot,
      interactionMode,
      selectedInventoryItemId,
      selectedInventoryItem
    );
    const kind = classifyHotspot(hotspot);
    const interactable =
      !disabled &&
      isHotspotInteractable(hotspot) &&
      (!hotspot.locked || ((kind === "door" || kind === "lock") && interactionMode === "use" && targetable));
    const isSelectionMode = interactionMode === "use" || interactionMode === "combine";
    const isHovered = hoveredHotspotId === hotspot.id;
    const strokeColor =
      isSelectionMode && selectedInventoryItemId
        ? targetable
          ? "#22d3ee"
          : "#475569"
        : isHovered && interactable
          ? "#f8fafc"
          : undefined;
    const hoverPulse = 0.5 + 0.5 * Math.sin(animTick / 3.6);
    const unlockStartedAt = unlockFlashes[hotspot.id];
    const unlockAge = unlockStartedAt ? Date.now() - unlockStartedAt : null;
    const unlockAlpha = unlockAge === null ? 0 : Math.max(0, 1 - unlockAge / 900);
    const hoverLabel = interactable
      ? `${hotspot.name} • ${getHotspotPrimaryActionLabel(hotspot)}${selectedInventoryItemId ? " • Use" : ""}`
      : hotspot.locked
        ? `${hotspot.name} • Locked`
        : !hotspot.available
          ? `${hotspot.name} • Unavailable`
          : `${hotspot.name} • Hidden`;
    const queuePrimaryAction = () => {
      const existingTimer = inspectTimersRef.current[hotspot.id];
      if (existingTimer) {
        window.clearTimeout(existingTimer);
      }

      inspectTimersRef.current[hotspot.id] = window.setTimeout(() => {
        if (isHotspotInteractable(hotspot)) {
          if (shouldPrimaryActionPickup(hotspot)) {
            onPickup(hotspot.id);
          } else {
            onInspect(hotspot.id);
          }
        }
        delete inspectTimersRef.current[hotspot.id];
      }, 170);
    };
    const triggerPickup = () => {
      const existingTimer = inspectTimersRef.current[hotspot.id];
      if (existingTimer) {
        window.clearTimeout(existingTimer);
        delete inspectTimersRef.current[hotspot.id];
      }

      if (!disabled && isHotspotInteractable(hotspot) && !hotspot.locked) {
        onPickup(hotspot.id);
      }
    };
    const commonShapeProps = {
      id: `hotspot-hit-${hotspot.id}`,
      x: frame.x,
      y: frame.y,
      width: frame.width,
      height: frame.height,
      fill: toAlphaColor(hotspot.color, 0.001),
      stroke: strokeColor,
      strokeWidth: strokeColor ? 2 : 0,
      listening: true,
      hitStrokeWidth: 8,
      onMouseEnter: () => {
        if (!disabled) {
          setHoveredHotspotId(hotspot.id);
        }
      },
      onTouchStart: () => {
        if (!disabled) {
          setHoveredHotspotId(hotspot.id);
          onHotspotFocus?.(hotspot.id);
        }
      },
      onMouseLeave: () => setHoveredHotspotId((current) => (current === hotspot.id ? null : current)),
      onTouchEnd: () => setHoveredHotspotId((current) => (current === hotspot.id ? null : current)),
      onClick: () => {
        if (disabled) {
          return;
        }
        onHotspotFocus?.(hotspot.id);
        if (interactable) {
          queuePrimaryAction();
        }
      },
      onTap: () => {
        if (disabled) {
          return;
        }
        onHotspotFocus?.(hotspot.id);
        if (interactable) {
          queuePrimaryAction();
        }
      },
      onDblClick: triggerPickup,
      onDblTap: triggerPickup,
    };

    return (
      <React.Fragment key={hotspot.id}>
        {renderHotspotObject(hotspot, interactable, theme, isHovered ? hoverPulse : 0.15, unlockAlpha, frame, {
          nodeId: `hotspot-visual-${hotspot.id}`,
          rotation: hotspot.id === "door-note" && theme.id === "clocktower" ? -8 : 0,
        })}
        {hotspot.hitArea === "ellipse" ? (
          <EllipseNode
            {...commonShapeProps}
            x={frame.x + frame.width / 2}
            y={frame.y + frame.height / 2}
            radiusX={frame.width / 2}
            radiusY={frame.height / 2}
          />
        ) : (
          <Rect {...commonShapeProps} cornerRadius={6} opacity={0.001} />
        )}
        {isHovered && !disabled && (
          <>
            <Rect
              x={frame.x}
              y={Math.max(8, frame.y - 26)}
              width={Math.min(220, Math.max(120, hotspot.name.length * 7 + 52))}
              height={22}
              fill="rgba(15, 23, 42, 0.88)"
              cornerRadius={6}
            />
            <Text
              text={hoverLabel}
              x={frame.x + 8}
              y={Math.max(11, frame.y - 22)}
              fill="#e2e8f0"
              fontSize={11}
            />
          </>
        )}
      </React.Fragment>
    );
  };

  return (
    <div ref={containerRef} className="rounded border border-slate-700 bg-slate-950 p-2">
      <StageNode width={room.width * scale} height={room.height * scale} scaleX={scale} scaleY={scale}>
        <LayerNode>
          <Rect x={0} y={0} width={room.width} height={room.height} fill={room.backgroundColor} />
          <Text text={room.roomName} x={12} y={10} fill="#e2e8f0" fontSize={18} />

          {sortedAssets
            .filter((asset) => asset.visible)
            .map((asset: RoomAsset) => <CanvasAsset key={`asset-${asset.id}`} asset={asset} theme={theme} />)}

          {sortedLayers
            .filter((layer) => layer.visible)
            .map((layer: RoomLayer) => <CanvasLayerVisual key={`layer-${layer.id}`} layer={layer} room={room} theme={theme} />)}

          {baseHotspots.map((hotspot) => renderHotspotEntry(hotspot))}
          {Object.entries(attachmentAnimations).map(([hotspotId, animation]) => {
            const elapsed = Date.now() - animation.startedAt;
            const progress = Math.min(1, elapsed / LOCK_OPEN_ANIMATION_MS);

            return (
              <React.Fragment key={`animation-${hotspotId}`}>
                {renderHotspotObject(animation.hotspot, false, theme, 0, 0, animation.frame, {
                  nodeId: `hotspot-animation-${hotspotId}`,
                  opacity: 1 - progress,
                  rotation: progress * 14,
                  unlockProgress: progress,
                })}
              </React.Fragment>
            );
          })}
          {pickupBursts.map((burst) => {
            const elapsed = Date.now() - burst.createdAt;
            const progress = Math.min(1, elapsed / 800);
            const radius = 12 + progress * 28;
            return (
              <CircleNode
                key={burst.id}
                x={burst.x}
                y={burst.y}
                radius={radius}
                stroke={theme.objectStroke}
                strokeWidth={2}
                opacity={1 - progress}
              />
            );
          })}
          <Rect
            x={0}
            y={0}
            width={room.width}
            height={room.height}
            fill={theme.ambientShadow}
            opacity={0.08 + 0.04 * (0.5 + 0.5 * Math.sin(animTick / 14))}
            listening={false}
          />
          {overlayHotspots.map((hotspot) => renderHotspotEntry(hotspot))}
        </LayerNode>
      </StageNode>
      <p className="mt-2 text-xs text-slate-300">
        {disabled
          ? "The room is no longer interactive."
          : "Hover objects to preview actions. Click to inspect, double-click to pick up, and use inventory mode to use items."}
      </p>
    </div>
  );
};

export default RoomCanvas;
