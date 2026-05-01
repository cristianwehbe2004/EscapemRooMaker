import React from "react";
import { SyncState } from "../../store/gameStore";

type ReconnectBannerProps = {
  syncState: SyncState;
  replayedDiffCount?: number;
  showSynced?: boolean;
};

const ReconnectBanner: React.FC<ReconnectBannerProps> = ({
  syncState,
  replayedDiffCount = 0,
  showSynced = false,
}) => {
  if (syncState === "synced" && !showSynced) {
    return null;
  }

  const messageByState: Record<SyncState, string> = {
    reconnecting: "Reconnecting to the session...",
    recovering: "Connected. Recovering missed updates...",
    replaying: `Replaying ${replayedDiffCount} missed change${replayedDiffCount === 1 ? "" : "s"}...`,
    synced: "Session synced. You are up to date.",
  };

  const styleByState: Record<SyncState, string> = {
    reconnecting: "border-amber-700 bg-amber-950 text-amber-100",
    recovering: "border-sky-700 bg-sky-950 text-sky-100",
    replaying: "border-cyan-700 bg-cyan-950 text-cyan-100",
    synced: "border-emerald-700 bg-emerald-950 text-emerald-100",
  };

  return (
    <div className={`rounded border px-4 py-3 text-sm ${styleByState[syncState]}`}>
      {messageByState[syncState]}
    </div>
  );
};

export default ReconnectBanner;
