import React from "react";
import { Layer, Rect, Stage, Text } from "react-konva";
import { RoomState } from "../../store/gameStore";

type RoomCanvasProps = {
  room: RoomState;
  onInspect: (targetId: string) => void;
  onPickup: (targetId: string) => void;
  highlightedTargetId?: string | null;
};

const StageNode = Stage as unknown as React.ComponentType<
  React.PropsWithChildren<{ width: number; height: number }>
>;
const LayerNode = Layer as unknown as React.ComponentType<React.PropsWithChildren<object>>;

const RoomCanvas: React.FC<RoomCanvasProps> = ({ room, onInspect, onPickup, highlightedTargetId }) => {
  return (
    <div className="rounded border border-slate-300 bg-slate-950 p-2">
      <StageNode width={room.width} height={room.height}>
        <LayerNode>
          <Rect x={0} y={0} width={room.width} height={room.height} fill="#0b1220" />
          <Text text={room.roomName} x={12} y={10} fill="#e2e8f0" fontSize={18} />

          {room.interactables
            .filter((item) => item.available && item.visible !== false)
            .map((item) => (
              <React.Fragment key={item.id}>
                <Rect
                  x={item.x}
                  y={item.y}
                  width={item.width}
                  height={item.height}
                  fill={item.color}
                  cornerRadius={6}
                  stroke={highlightedTargetId ? "#22d3ee" : undefined}
                  strokeWidth={highlightedTargetId ? 2 : 0}
                  onClick={() => onInspect(item.id)}
                  onDblClick={() => onPickup(item.id)}
                />
                <Text text={item.name} x={item.x + 4} y={item.y + 4} fill="#0f172a" fontSize={12} />
              </React.Fragment>
            ))}
        </LayerNode>
      </StageNode>
      <p className="mt-2 text-xs text-slate-300">Single click: inspect | Double click: pickup</p>
    </div>
  );
};

export default RoomCanvas;
