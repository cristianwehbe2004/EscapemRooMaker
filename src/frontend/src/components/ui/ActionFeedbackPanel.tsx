import React from "react";
import { ActionError } from "../../types/actionError";

type CooldownChip = {
  key: string;
  label: string;
  remainingMs: number;
};

type ActionFeedbackPanelProps = {
  lastActionLabel: string | null;
  pendingActionLabel: string | null;
  actionError: ActionError | null;
  cooldownChips?: CooldownChip[];
  messages: string[];
};

const ActionFeedbackPanel: React.FC<ActionFeedbackPanelProps> = ({
  lastActionLabel,
  pendingActionLabel,
  actionError,
  cooldownChips = [],
  messages,
}) => {
  const recentMessages = messages.slice(-5).reverse();

  const errorSourceLabel =
    actionError?.source === "local-cooldown"
      ? "Local cooldown"
      : actionError?.source === "server-rate-limit"
        ? "Server rate limit"
        : actionError?.source === "network"
          ? "Network"
          : actionError?.source === "server"
            ? "Server"
            : null;

  return (
    <section className="rounded border border-slate-700 bg-slate-900/95 p-4">
      <h2 className="mb-3 text-lg font-semibold text-slate-100">Action Feedback</h2>
      <div className="mb-3 space-y-2 text-sm">
        <p className="text-slate-300">Pending: {pendingActionLabel ?? "None"}</p>
        <p className="text-slate-300">Last action: {lastActionLabel ?? "None"}</p>
        {actionError && (
          <div className="rounded bg-amber-950 px-2 py-1 text-amber-200">
            <p>{actionError.message}</p>
            {errorSourceLabel && <p className="text-xs text-amber-300">Source: {errorSourceLabel}</p>}
            {(actionError.retryAfterMs || actionError.policyName) && (
              <p className="text-xs text-amber-300">
                {actionError.retryAfterMs ? `Retry after ${actionError.retryAfterMs}ms.` : ""}
                {actionError.policyName ? ` Policy: ${actionError.policyName}.` : ""}
              </p>
            )}
          </div>
        )}
        {cooldownChips.length > 0 && (
          <div className="flex flex-wrap gap-2">
            {cooldownChips.map((chip) => (
              <span key={chip.key} className="rounded bg-slate-800 px-2 py-1 text-xs text-slate-200">
                {chip.label}: {Math.ceil(chip.remainingMs / 1000)}s
              </span>
            ))}
          </div>
        )}
      </div>

      <div>
        <h3 className="mb-2 text-sm font-medium text-slate-200">Recent messages</h3>
        {recentMessages.length === 0 ? (
          <p className="text-sm text-slate-400">No server messages yet.</p>
        ) : (
          <ul className="flex flex-col gap-2">
            {recentMessages.map((message, index) => (
              <li key={`${message}-${index}`} className="rounded bg-slate-800 px-3 py-2 text-sm text-slate-200">
                {message}
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  );
};

export default ActionFeedbackPanel;
