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
  id: "clocktower" | "crypt" | "default";
  ambientShadow: string;
  objectStroke: string;
  warmLight: string;
  coolLight: string;
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

  return {
    id: "default",
    ambientShadow: "rgba(2, 6, 23, 0.25)",
    objectStroke: "#cbd5e1",
    warmLight: "rgba(245, 158, 11, 0.18)",
    coolLight: "rgba(96, 165, 250, 0.12)",
  };
};

type HotspotKind = "key" | "door" | "note" | "chest" | "drawer" | "lock" | "switch" | "generic";

const classifyHotspot = (hotspot: RoomHotspot): HotspotKind => {
  const explicit = hotspot.visualKind?.toLowerCase();
  if (explicit === "key" || explicit === "door" || explicit === "note" || explicit === "chest" || explicit === "drawer" || explicit === "lock" || explicit === "switch") {
    return explicit;
  }

  const value = `${hotspot.id} ${hotspot.name}`.toLowerCase();
  if (value.includes("key")) return "key";
  if (value.includes("door") || value.includes("gate")) return "door";
  if (value.includes("note") || value.includes("panel") || value.includes("book")) return "note";
  if (value.includes("drawer")) return "drawer";
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

  if (kind === "key") {
    return "Pick up";
  }

  return "Inspect";
};

const renderHotspotObject = (
  hotspot: RoomHotspot,
  active: boolean,
  theme: ThemePack,
  pulse: number,
  unlockAlpha: number
) => {
  const kind = classifyHotspot(hotspot);
  const x = hotspot.x;
  const y = hotspot.y;
  const w = hotspot.width;
  const h = hotspot.height;
  const glow = active ? 0.86 + pulse * 0.22 : 0.58;
  const lockTint = hotspot.locked ? 0.5 : 1;

  if (kind === "key") {
    return (
      <GroupNode x={x} y={y}>
        <CircleNode x={w * 0.22} y={h * 0.5} radius={Math.max(8, Math.min(w, h) * 0.2)} stroke="#f8d66d" strokeWidth={5 * glow} opacity={lockTint} />
        <LineNode points={[w * 0.33, h * 0.5, w * 0.9, h * 0.5]} stroke="#f8d66d" strokeWidth={6 * glow} lineCap="round" opacity={lockTint} />
        <LineNode points={[w * 0.72, h * 0.5, w * 0.72, h * 0.7, w * 0.64, h * 0.7]} stroke="#f8d66d" strokeWidth={4 * glow} opacity={lockTint} />
      </GroupNode>
    );
  }

  if (kind === "door") {
    return (
      <GroupNode x={x} y={y}>
        <Rect x={0} y={0} width={w} height={h} fill={hotspot.locked ? "#4b2d22" : "#6a4c34"} cornerRadius={4} />
        <Rect x={w * 0.08} y={h * 0.08} width={w * 0.84} height={h * 0.84} stroke="#8d6551" strokeWidth={2} />
        <CircleNode x={w * 0.78} y={h * 0.52} radius={Math.max(3, Math.min(w, h) * 0.04)} fill="#d6b06a" />
        {!hotspot.locked && <Rect x={w * 0.05} y={0} width={w * 0.9} height={h} fill="rgba(253, 224, 71, 0.12)" cornerRadius={4} />}
      </GroupNode>
    );
  }

  if (kind === "note") {
    return (
      <GroupNode x={x} y={y}>
        <Rect x={0} y={0} width={w} height={h} fill={hotspot.color || "#f4edd2"} stroke="#7c5b17" strokeWidth={2} cornerRadius={3} />
        <CircleNode x={w * 0.5} y={h * 0.12} radius={Math.max(2, w * 0.05)} fill="#7c2d12" />
        <LineNode points={[w * 0.1, h * 0.25, w * 0.85, h * 0.25]} stroke="#8b7d58" strokeWidth={2} />
        <LineNode points={[w * 0.1, h * 0.45, w * 0.78, h * 0.45]} stroke="#8b7d58" strokeWidth={2} />
        <LineNode points={[w * 0.1, h * 0.65, w * 0.82, h * 0.65]} stroke="#8b7d58" strokeWidth={2} />
      </GroupNode>
    );
  }

  if (kind === "drawer") {
    return (
      <GroupNode x={x} y={y}>
        <Rect x={0} y={0} width={w} height={h} fill={hotspot.locked ? "#65452f" : "#8a6141"} cornerRadius={6} />
        <Rect x={w * 0.08} y={h * 0.2} width={w * 0.84} height={h * 0.6} stroke="#c68b59" strokeWidth={2} cornerRadius={4} />
        <CircleNode x={w * 0.5} y={h * 0.5} radius={Math.max(4, Math.min(w, h) * 0.08)} fill={hotspot.locked ? "#8b6b3f" : "#f1c27d"} />
      </GroupNode>
    );
  }

  if (kind === "lock") {
    return (
      <GroupNode x={x} y={y}>
        <Rect x={w * 0.15} y={h * 0.32} width={w * 0.7} height={h * 0.58} fill={hotspot.locked ? "#d4a34f" : "#64748b"} cornerRadius={5} />
        <LineNode
          points={[w * 0.3, h * 0.34, w * 0.3, h * 0.16, w * 0.5, h * 0.06, w * 0.7, h * 0.16, w * 0.7, h * 0.34]}
          stroke={hotspot.locked ? "#f6d365" : "#94a3b8"}
          strokeWidth={4}
          lineCap="round"
          lineJoin="round"
        />
      </GroupNode>
    );
  }

  if (kind === "chest") {
    return (
      <GroupNode x={x} y={y}>
        <Rect x={0} y={h * 0.24} width={w} height={h * 0.76} fill={hotspot.locked ? "#5b3f2c" : "#7a5636"} cornerRadius={5} />
        <Rect x={0} y={0} width={w} height={h * 0.38} fill={hotspot.locked ? "#704b33" : "#926543"} cornerRadius={5} />
        <Rect x={w * 0.46} y={h * 0.48} width={w * 0.08} height={h * 0.2} fill={hotspot.locked ? "#9c7a3d" : "#e7c57d"} />
      </GroupNode>
    );
  }

  if (kind === "switch") {
    return (
      <GroupNode x={x} y={y}>
        <Rect x={0} y={0} width={w} height={h} fill="#374151" cornerRadius={6} />
        <LineNode points={[w * 0.4, h * 0.75, w * 0.6, h * 0.3]} stroke="#e5e7eb" strokeWidth={5} lineCap="round" />
      </GroupNode>
    );
  }

  return (
    <GroupNode x={x} y={y}>
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
  const prevHotspotsRef = useRef<Map<string, RoomHotspot>>(new Map());
  const inspectTimersRef = useRef<Record<string, number>>({});
  const theme = useMemo(() => resolveThemePack(room.themeId, room.roomName), [room.roomName, room.themeId]);
  const scale = useMemo(() => Math.min(1, Math.max(0.25, containerWidth / room.width)), [containerWidth, room.width]);

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
      const prev = previous.get(hotspot.id);
      if (prev) {
        if (prev.locked && !hotspot.locked) {
          setUnlockFlashes((current) => ({ ...current, [hotspot.id]: now }));
        }

        if (prev.visible && !hotspot.visible) {
          setPickupBursts((current) => [
            ...current,
            { id: `${hotspot.id}-${now}`, x: hotspot.x + hotspot.width / 2, y: hotspot.y + hotspot.height / 2, createdAt: now },
          ]);
        }
      }

      next.set(hotspot.id, { ...hotspot });
    }

    prevHotspotsRef.current = next;
  }, [room.hotspots]);

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
  }, [animTick]);

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

          {room.hotspots
            .filter((hotspot) => hotspot.visible)
            .map((hotspot) => {
              const targetable = isTargetableForMode(
                hotspot,
                interactionMode,
                selectedInventoryItemId,
                selectedInventoryItem
              );
              const interactable = !disabled && isHotspotInteractable(hotspot);
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
              const queueInspect = () => {
                const existingTimer = inspectTimersRef.current[hotspot.id];
                if (existingTimer) {
                  window.clearTimeout(existingTimer);
                }

                inspectTimersRef.current[hotspot.id] = window.setTimeout(() => {
                  if (isHotspotInteractable(hotspot)) {
                    onInspect(hotspot.id);
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

                if (!disabled && isHotspotInteractable(hotspot)) {
                  onPickup(hotspot.id);
                }
              };
              const commonShapeProps = {
                x: hotspot.x,
                y: hotspot.y,
                width: hotspot.width,
                height: hotspot.height,
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
                    queueInspect();
                  }
                },
                onTap: () => {
                  if (disabled) {
                    return;
                  }
                  onHotspotFocus?.(hotspot.id);
                  if (interactable) {
                    queueInspect();
                  }
                },
                onDblClick: triggerPickup,
                onDblTap: triggerPickup,
              };

              return (
                <React.Fragment key={hotspot.id}>
                  {renderHotspotObject(hotspot, interactable, theme, isHovered ? hoverPulse : 0.15, unlockAlpha)}
                  {hotspot.hitArea === "ellipse" ? (
                    <EllipseNode
                      {...commonShapeProps}
                      x={hotspot.x + hotspot.width / 2}
                      y={hotspot.y + hotspot.height / 2}
                      radiusX={hotspot.width / 2}
                      radiusY={hotspot.height / 2}
                    />
                  ) : (
                    <Rect {...commonShapeProps} cornerRadius={6} opacity={0.001} />
                  )}
                  {isHovered && !disabled && (
                    <>
                      <Rect
                        x={hotspot.x}
                        y={Math.max(8, hotspot.y - 26)}
                        width={Math.min(220, Math.max(120, hotspot.name.length * 7 + 52))}
                        height={22}
                        fill="rgba(15, 23, 42, 0.88)"
                        cornerRadius={6}
                      />
                      <Text
                        text={hoverLabel}
                        x={hotspot.x + 8}
                        y={Math.max(11, hotspot.y - 22)}
                        fill="#e2e8f0"
                        fontSize={11}
                      />
                    </>
                  )}
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
