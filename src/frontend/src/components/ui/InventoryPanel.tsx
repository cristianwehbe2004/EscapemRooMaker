import React from "react";
import { InventoryItem } from "../../store/gameStore";

export type InventoryInteractionMode = "none" | "use" | "combine";

type InventoryPanelProps = {
  items: InventoryItem[];
  selectedItemId: string | null;
  interactionMode: InventoryInteractionMode;
  onItemClick: (itemId: string) => void;
  onSetInteractionMode: (mode: InventoryInteractionMode) => void;
  onClearSelection: () => void;
  disabled?: boolean;
};

const InventoryPanel: React.FC<InventoryPanelProps> = ({
  items,
  selectedItemId,
  interactionMode,
  onItemClick,
  onSetInteractionMode,
  onClearSelection,
  disabled = false,
}) => {
  const selectedItem = items.find((item) => item.id === selectedItemId) ?? null;
  const canActOnSelected = Boolean(selectedItem) && !disabled;
  const selectedItemReady = selectedItem?.status === "ready";
  const canUseSelected =
    canActOnSelected && selectedItemReady && (!selectedItem?.usableTargetIds || selectedItem.usableTargetIds.length > 0);
  const canCombineSelected =
    canActOnSelected &&
    selectedItemReady &&
    (!selectedItem?.combinableWithIds || selectedItem.combinableWithIds.length > 0);

  return (
    <aside className="rounded border border-slate-700 bg-slate-900/95 p-4">
      <div className="mb-3 flex items-center justify-between">
        <h2 className="text-lg font-semibold text-slate-100">Inventory</h2>
        <span className="rounded bg-slate-800 px-2 py-1 text-xs text-slate-300">{items.length} items</span>
      </div>

      <div className="mb-3 space-y-2 text-xs text-slate-300">
        <p>Selected: {selectedItem?.label ?? "None"}</p>
        <p>Mode: {interactionMode}</p>
        {selectedItem && <p>Type: {selectedItem.type}</p>}
        {selectedItem && <p>Status: {selectedItem.status}</p>}
      </div>

      <div className="mb-4 flex flex-wrap gap-2">
        <button
          disabled={!canUseSelected}
          onClick={() => onSetInteractionMode(interactionMode === "use" ? "none" : "use")}
          className="rounded bg-indigo-700 px-2 py-1 text-xs text-white disabled:cursor-not-allowed disabled:opacity-40"
        >
          {interactionMode === "use" ? "Cancel Use" : "Use"}
        </button>
        <button
          disabled={!canCombineSelected}
          onClick={() => onSetInteractionMode(interactionMode === "combine" ? "none" : "combine")}
          className="rounded bg-amber-700 px-2 py-1 text-xs text-white disabled:cursor-not-allowed disabled:opacity-40"
        >
          {interactionMode === "combine" ? "Cancel Combine" : "Combine"}
        </button>
        <button
          disabled={disabled}
          onClick={onClearSelection}
          className="rounded bg-slate-700 px-2 py-1 text-xs text-white disabled:cursor-not-allowed disabled:opacity-40"
        >
          Clear
        </button>
      </div>

      {items.length === 0 ? (
        <p className="text-sm text-slate-400">No items collected yet.</p>
      ) : (
        <ul className="flex flex-col gap-2">
          {items.map((item) => (
            <li key={item.id}>
              <button
                disabled={disabled}
                onClick={() => onItemClick(item.id)}
                className={`w-full rounded border px-3 py-2 text-left text-sm text-slate-100 ${
                  selectedItemId === item.id
                    ? "border-emerald-500 bg-emerald-950"
                    : "border-slate-700 bg-slate-800"
                } disabled:cursor-not-allowed disabled:opacity-40`}
              >
                <span>{item.label}</span>
                {item.quantity > 1 && <span className="ml-2 text-xs text-slate-300">x{item.quantity}</span>}
                <span className="ml-2 rounded bg-slate-700 px-1.5 py-0.5 text-[10px] uppercase tracking-wide text-slate-200">
                  {item.type}
                </span>
                <span className="ml-2 rounded bg-slate-700 px-1.5 py-0.5 text-[10px] uppercase tracking-wide text-slate-200">
                  {item.status}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}

      {interactionMode === "use" && selectedItem && (
        <p className="mt-3 rounded bg-indigo-950 px-2 py-1 text-xs text-indigo-200">
          Click a hotspot in the room to use {selectedItem.label}.
        </p>
      )}
      {interactionMode === "combine" && selectedItem && (
        <p className="mt-3 rounded bg-amber-950 px-2 py-1 text-xs text-amber-200">
          Select another inventory item to combine with {selectedItem.label}.
        </p>
      )}
    </aside>
  );
};

export default InventoryPanel;
