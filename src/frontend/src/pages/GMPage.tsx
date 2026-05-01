import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { GameRealtimeClient } from "../realtime/gameRealtimeClient";
import { useGameStore } from "../store/gameStore";
import { GmSessionSummary, PlayerPresenceEvent, SessionTimelineEntry } from "../types/realtime";

const apiBaseUrl = process.env.REACT_APP_API_BASE_URL ?? "http://localhost:5000";

const createClientActionId = (): string => {
	if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
		return crypto.randomUUID();
	}

	return `${Date.now()}-${Math.random().toString(16).slice(2)}`;
};

const formatTimestamp = (value: string | null | undefined): string => {
	if (!value) {
		return "-";
	}

	const date = new Date(value);
	return Number.isNaN(date.getTime()) ? "-" : date.toLocaleString();
};

const GMPage: React.FC = () => {
	const {
		applyDiff,
		applySnapshot,
		connected,
		syncState,
		sessionId,
		setConnectionStatus,
		setSyncState,
		setSessionId,
		sessionVersion,
		state,
	} = useGameStore();

	const [accessToken, setAccessToken] = useState("");
	const [selectedSessionId, setSelectedSessionId] = useState("");
	const [sessions, setSessions] = useState<GmSessionSummary[]>([]);
	const [timeline, setTimeline] = useState<SessionTimelineEntry[]>([]);
	const [presenceByPlayer, setPresenceByPlayer] = useState<Record<string, PlayerPresenceEvent>>({});
	const [hintText, setHintText] = useState("");
	const [hintTarget, setHintTarget] = useState("");
	const [broadcastText, setBroadcastText] = useState("");
	const [revealPuzzleId, setRevealPuzzleId] = useState("");
	const [controlTarget, setControlTarget] = useState("");
	const [error, setError] = useState<string | null>(null);
	const [statusMessage, setStatusMessage] = useState<string | null>(null);

	const sessionRef = useRef<string | null>(sessionId);
	sessionRef.current = sessionId;

	const client = useMemo(
		() =>
			new GameRealtimeClient(
				{
					baseUrl: apiBaseUrl,
					getAccessToken: () => accessToken,
				},
				{
					onDiff: (diff) => {
						applyDiff(diff);
					},
					onSnapshot: (snapshot) => {
						applySnapshot(snapshot);
						if (snapshot.playerPresence) {
							setPresenceByPlayer(() => {
								const next: Record<string, PlayerPresenceEvent> = {};
								snapshot.playerPresence?.forEach((entry) => {
									next[entry.playerId] = entry;
								});
								return next;
							});
						}
					},
					onPresenceChanged: (entry) => {
						setPresenceByPlayer((current) => ({
							...current,
							[entry.playerId]: entry,
						}));
					},
					onReconnecting: () => {
						setConnectionStatus({ connected: false });
						setSyncState("reconnecting");
					},
					onReconnected: () => {
						setConnectionStatus({ connected: true });
						setSyncState("synced");
					},
					onDisconnected: () => setConnectionStatus({ connected: false }),
				}
			),
		[accessToken, applyDiff, applySnapshot, setConnectionStatus, setSyncState]
	);

	useEffect(() => {
		return () => {
			void client.stop();
		};
	}, [client]);

	const refreshActiveSessions = async () => {
		try {
			setError(null);
			const result = await client.getActiveSessions();
			setSessions(result);
			if (!selectedSessionId && result.length > 0) {
				setSelectedSessionId(result[0].sessionId);
			}
			setStatusMessage(`Loaded ${result.length} session records.`);
		} catch (nextError) {
			setError(nextError instanceof Error ? nextError.message : "Failed to load active sessions.");
		}
	};

	const refreshTimeline = useCallback(async (targetSessionId: string) => {
		const nextTimeline = await client.getSessionTimeline(targetSessionId, 120);
		setTimeline(nextTimeline);
	}, [client]);

	const refreshPresence = useCallback(async (targetSessionId: string) => {
		const nextPresence = await client.getPlayerPresence(targetSessionId);
		setPresenceByPlayer(() => {
			const map: Record<string, PlayerPresenceEvent> = {};
			nextPresence.forEach((entry) => {
				map[entry.playerId] = entry;
			});
			return map;
		});
	}, [client]);

	const joinSession = async () => {
		if (!selectedSessionId) {
			setError("Select or enter a session id first.");
			return;
		}

		try {
			setError(null);
			const ack = await client.start(selectedSessionId);
			setSessionId(ack.sessionId);
			setConnectionStatus({ connected: true });
			setSyncState("synced");
			await Promise.all([refreshTimeline(ack.sessionId), refreshPresence(ack.sessionId)]);
			setStatusMessage(`Connected to session ${ack.sessionId}.`);
		} catch (nextError) {
			setConnectionStatus({ connected: false });
			setError(nextError instanceof Error ? nextError.message : "Failed to join session.");
		}
	};

	const sendHint = async () => {
		if (!sessionRef.current || !hintText.trim()) {
			return;
		}

		await client.submitGmHint(sessionRef.current, {
			hint: hintText.trim(),
			scope: hintTarget.trim() ? "targeted" : "session",
			target: hintTarget.trim() || undefined,
			clientActionId: createClientActionId(),
		});
		setHintText("");
		await refreshTimeline(sessionRef.current);
	};

	const sendBroadcast = async () => {
		if (!sessionRef.current || !broadcastText.trim()) {
			return;
		}

		await client.broadcastMessage(sessionRef.current, broadcastText.trim(), controlTarget.trim() || undefined);
		setBroadcastText("");
		await refreshTimeline(sessionRef.current);
	};

	const forceSync = async () => {
		if (!sessionRef.current) {
			return;
		}

		await client.forceSyncSession(sessionRef.current);
		await Promise.all([refreshTimeline(sessionRef.current), refreshPresence(sessionRef.current)]);
		setStatusMessage("Force sync action submitted.");
	};

	const revealPuzzle = async () => {
		if (!sessionRef.current || !revealPuzzleId.trim()) {
			return;
		}

		await client.revealPuzzle(sessionRef.current, revealPuzzleId.trim(), controlTarget.trim() || undefined);
		setRevealPuzzleId("");
		await refreshTimeline(sessionRef.current);
	};

	useEffect(() => {
		if (!sessionId || !connected) {
			return;
		}

		const intervalId = window.setInterval(() => {
			void refreshTimeline(sessionId);
		}, 5000);

		return () => window.clearInterval(intervalId);
	}, [connected, refreshTimeline, sessionId]);

	const presenceRows = Object.values(presenceByPlayer).sort((a, b) => a.displayName.localeCompare(b.displayName));

	return (
		<main className="mx-auto flex max-w-7xl flex-col gap-4 p-4 text-slate-100">
			<header className="rounded-lg border border-slate-700 bg-slate-900 p-4">
				<h1 className="text-2xl font-semibold">GM Panel (Day 5)</h1>
				<p className="mt-1 text-sm text-slate-300">
					Server-authoritative spectator view with presence, action timeline, hints, and admin controls.
				</p>
			</header>

			<section className="grid gap-3 rounded-lg border border-slate-700 bg-slate-900 p-4 lg:grid-cols-[2fr_1fr_1fr]">
				<input
					value={accessToken}
					onChange={(event) => setAccessToken(event.target.value)}
					placeholder="GM/Admin bearer token"
					className="rounded border border-slate-600 bg-slate-800 px-3 py-2"
				/>
				<input
					value={selectedSessionId}
					onChange={(event) => setSelectedSessionId(event.target.value)}
					placeholder="Session UUID"
					className="rounded border border-slate-600 bg-slate-800 px-3 py-2"
				/>
				<div className="flex flex-wrap gap-2">
					<button onClick={refreshActiveSessions} className="rounded bg-slate-700 px-3 py-2 text-sm">
						Load Sessions
					</button>
					<button onClick={joinSession} className="rounded bg-blue-600 px-3 py-2 text-sm text-white">
						Join
					</button>
				</div>
			</section>

			<p className="text-sm text-slate-300">
				Status: {connected ? "connected" : "disconnected"} | Sync: {syncState} | Session: {sessionId ?? "none"} |
				Version: {sessionVersion}
			</p>

			{statusMessage && <p className="rounded border border-emerald-700 bg-emerald-950 p-2 text-sm text-emerald-200">{statusMessage}</p>}
			{error && <p className="rounded border border-red-700 bg-red-950 p-2 text-sm text-red-200">{error}</p>}

			<section className="rounded-lg border border-slate-700 bg-slate-900 p-4">
				<h2 className="mb-3 text-lg font-semibold">Session Dashboard</h2>
				<div className="overflow-x-auto">
					<table className="min-w-full text-left text-sm">
						<thead className="text-slate-300">
							<tr>
								<th className="px-2 py-2">Room</th>
								<th className="px-2 py-2">Session</th>
								<th className="px-2 py-2">Status</th>
								<th className="px-2 py-2">Connected</th>
								<th className="px-2 py-2">Started</th>
							</tr>
						</thead>
						<tbody>
							{sessions.map((entry) => (
								<tr
									key={entry.sessionId}
									className={`cursor-pointer border-t border-slate-800 ${selectedSessionId === entry.sessionId ? "bg-slate-800" : ""}`}
									onClick={() => setSelectedSessionId(entry.sessionId)}
								>
									<td className="px-2 py-2">{entry.roomName}</td>
									<td className="px-2 py-2">{entry.sessionId}</td>
									<td className="px-2 py-2">{entry.status}</td>
									<td className="px-2 py-2">{entry.connectedPlayers}</td>
									<td className="px-2 py-2">{formatTimestamp(entry.startedAtUtc)}</td>
								</tr>
							))}
						</tbody>
					</table>
				</div>
			</section>

			<section className="grid gap-4 lg:grid-cols-[1fr_1.2fr_1fr]">
				<article className="rounded-lg border border-slate-700 bg-slate-900 p-4">
					<h2 className="mb-3 text-lg font-semibold">Player Presence</h2>
					<ul className="space-y-2 text-sm">
						{presenceRows.length === 0 && <li className="text-slate-400">No presence data yet.</li>}
						{presenceRows.map((entry) => (
							<li key={entry.playerId} className="rounded border border-slate-700 bg-slate-800 p-2">
								<p className="font-medium">{entry.displayName}</p>
								<p className="text-slate-300">Player: {entry.playerId}</p>
								<p className="text-slate-300">State: {entry.status}</p>
								<p className="text-slate-400">Last seen: {formatTimestamp(entry.lastSeenUtc)}</p>
							</li>
						))}
					</ul>
				</article>

				<article className="rounded-lg border border-slate-700 bg-slate-900 p-4">
					<div className="mb-3 flex items-center justify-between">
						<h2 className="text-lg font-semibold">Action Timeline</h2>
						<button
							disabled={!sessionId}
							onClick={() => sessionId && void refreshTimeline(sessionId)}
							className="rounded bg-slate-700 px-3 py-1 text-xs disabled:cursor-not-allowed disabled:opacity-50"
						>
							Refresh
						</button>
					</div>
					<ul className="max-h-[420px] space-y-2 overflow-auto text-sm">
						{timeline.length === 0 && <li className="text-slate-400">No timeline entries yet.</li>}
						{timeline.map((entry) => (
							<li key={`${entry.sequenceNumber}-${entry.occurredAtUtc}`} className="rounded border border-slate-700 bg-slate-800 p-2">
								<p className="font-medium">#{entry.sequenceNumber} {entry.eventType}</p>
								<p className="text-slate-300">{entry.summary}</p>
								<p className="text-slate-400">{entry.actor} at {formatTimestamp(entry.occurredAtUtc)}</p>
							</li>
						))}
					</ul>
				</article>

				<article className="rounded-lg border border-slate-700 bg-slate-900 p-4">
					<h2 className="mb-3 text-lg font-semibold">GM Controls</h2>

					<div className="mb-4 space-y-2">
						<h3 className="text-sm font-medium text-slate-300">Hint</h3>
						<textarea
							value={hintText}
							onChange={(event) => setHintText(event.target.value)}
							placeholder="Hint text"
							className="min-h-20 w-full rounded border border-slate-600 bg-slate-800 p-2"
						/>
						<input
							value={hintTarget}
							onChange={(event) => setHintTarget(event.target.value)}
							placeholder="Target player id (optional)"
							className="w-full rounded border border-slate-600 bg-slate-800 px-3 py-2"
						/>
						<button
							disabled={!sessionId}
							onClick={() => void sendHint()}
							className="rounded bg-indigo-600 px-3 py-2 text-sm text-white disabled:cursor-not-allowed disabled:opacity-50"
						>
							Send Hint
						</button>
					</div>

					<div className="mb-4 space-y-2">
						<h3 className="text-sm font-medium text-slate-300">Broadcast</h3>
						<textarea
							value={broadcastText}
							onChange={(event) => setBroadcastText(event.target.value)}
							placeholder="Broadcast message"
							className="min-h-16 w-full rounded border border-slate-600 bg-slate-800 p-2"
						/>
						<input
							value={controlTarget}
							onChange={(event) => setControlTarget(event.target.value)}
							placeholder="Optional target"
							className="w-full rounded border border-slate-600 bg-slate-800 px-3 py-2"
						/>
						<button
							disabled={!sessionId}
							onClick={() => void sendBroadcast()}
							className="rounded bg-emerald-600 px-3 py-2 text-sm text-white disabled:cursor-not-allowed disabled:opacity-50"
						>
							Send Broadcast
						</button>
					</div>

					<div className="space-y-2">
						<h3 className="text-sm font-medium text-slate-300">Admin Controls</h3>
						<input
							value={revealPuzzleId}
							onChange={(event) => setRevealPuzzleId(event.target.value)}
							placeholder="Puzzle id to reveal"
							className="w-full rounded border border-slate-600 bg-slate-800 px-3 py-2"
						/>
						<div className="flex flex-wrap gap-2">
							<button
								disabled={!sessionId}
								onClick={() => void revealPuzzle()}
								className="rounded bg-amber-600 px-3 py-2 text-sm text-white disabled:cursor-not-allowed disabled:opacity-50"
							>
								Reveal
							</button>
							<button
								disabled={!sessionId}
								onClick={() => void forceSync()}
								className="rounded bg-rose-600 px-3 py-2 text-sm text-white disabled:cursor-not-allowed disabled:opacity-50"
							>
								Force Sync
							</button>
						</div>
					</div>
				</article>
			</section>

			<section className="rounded-lg border border-slate-700 bg-slate-900 p-4">
				<h2 className="mb-2 text-lg font-semibold">Realtime Messages</h2>
				<ul className="max-h-40 space-y-1 overflow-auto text-sm text-slate-300">
					{state.messages.map((message, index) => (
						<li key={`${message}-${index}`} className="rounded bg-slate-800 px-2 py-1">
							{message}
						</li>
					))}
				</ul>
			</section>
		</main>
	);
};

export default GMPage;
