import React, { useEffect, useMemo, useState } from "react";
import { useAuthSession } from "../auth/authSession";
import AuthPanel from "../components/auth/AuthPanel";
import { useNavigate } from "react-router-dom";
import RoomCanvas from "../components/konva/RoomCanvas";
import { EditorDocumentDto, TriggerGraphNode, ValidationIssueDto } from "../types/editor";
import { initialGameData } from "../store/gameStore";

const apiBaseUrl = process.env.REACT_APP_API_BASE_URL ?? "http://localhost:5130";

const defaultDocument: EditorDocumentDto = {
  room: initialGameData.room,
  triggerGraph: {
    version: 1,
    metadata: {},
    nodes: [],
    edges: [],
  },
};

const createId = (prefix: string): string => `${prefix}-${Date.now()}-${Math.random().toString(16).slice(2, 6)}`;

const EditorPage: React.FC = () => {
  const navigate = useNavigate();
  const { accessToken } = useAuthSession();
  const [roomId, setRoomId] = useState("");
  const [editorDoc, setEditorDoc] = useState<EditorDocumentDto>(defaultDocument);
  const [selectedHotspotId, setSelectedHotspotId] = useState<string | null>(null);
  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null);
  const [edgeFromNodeId, setEdgeFromNodeId] = useState("");
  const [edgeToNodeId, setEdgeToNodeId] = useState("");
  const [issues, setIssues] = useState<ValidationIssueDto[]>([]);
  const [status, setStatus] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const selectedHotspot = useMemo(
    () => editorDoc.room.hotspots.find((x) => x.id === selectedHotspotId) ?? null,
    [editorDoc.room.hotspots, selectedHotspotId]
  );

  const selectedNode = useMemo(
    () => editorDoc.triggerGraph.nodes.find((x) => x.nodeId === selectedNodeId) ?? null,
    [editorDoc.triggerGraph.nodes, selectedNodeId]
  );

  useEffect(() => {
    if (!selectedHotspotId) {
      return;
    }

    if (!editorDoc.room.hotspots.some((x) => x.id === selectedHotspotId)) {
      setSelectedHotspotId(null);
    }
  }, [editorDoc.room.hotspots, selectedHotspotId]);

  const callApi = async <T,>(path: string, method = "GET", body?: unknown): Promise<T> => {
    const response = await fetch(`${apiBaseUrl}${path}`, {
      method,
      headers: {
        "Content-Type": "application/json",
        ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}),
      },
      body: body ? JSON.stringify(body) : undefined,
    });

    if (!response.ok) {
      const text = await response.text();
      throw new Error(text || `Request failed: ${response.status}`);
    }

    return (await response.json()) as T;
  };

  const loadRoom = async () => {
    if (!roomId.trim()) {
      setStatus("Provide room id.");
      return;
    }

    setLoading(true);
    try {
      const loaded = await callApi<EditorDocumentDto>(`/api/creator/rooms/${roomId.trim()}/editor-document`);
      setEditorDoc(loaded);
      setIssues([]);
      setStatus("Editor document loaded.");
    } catch (error) {
      setStatus(error instanceof Error ? error.message : "Failed to load room.");
    } finally {
      setLoading(false);
    }
  };

  const validateDocument = async () => {
    if (!roomId.trim()) {
      return;
    }

    setLoading(true);
    try {
      const result = await callApi<{ isValid: boolean; issues: ValidationIssueDto[] }>(
        `/api/creator/rooms/${roomId.trim()}/validate`,
        "POST",
        { document: editorDoc }
      );
      setIssues(result.issues ?? []);
      setStatus(result.isValid ? "Validation passed." : `Validation failed: ${result.issues.length} issue(s).`);
    } catch (error) {
      setStatus(error instanceof Error ? error.message : "Validation failed.");
    } finally {
      setLoading(false);
    }
  };

  const saveDocument = async () => {
    if (!roomId.trim()) {
      return;
    }

    setLoading(true);
    try {
      const result = await callApi<{ versionNumber: number; issues: ValidationIssueDto[] }>(
        `/api/creator/rooms/${roomId.trim()}`,
        "PUT",
        { document: editorDoc }
      );
      setIssues(result.issues ?? []);
      setStatus(`Saved room version ${result.versionNumber}.`);
    } catch (error) {
      setStatus(error instanceof Error ? error.message : "Save failed.");
    } finally {
      setLoading(false);
    }
  };

  const createPlaytest = async () => {
    if (!roomId.trim()) {
      return;
    }

    setLoading(true);
    try {
      const result = await callApi<{ playerJoinPath: string }>(`/api/creator/rooms/${roomId.trim()}/playtest-sessions`, "POST");
      setStatus("Playtest session created. Redirecting to player...");
      navigate(result.playerJoinPath);
    } catch (error) {
      setStatus(error instanceof Error ? error.message : "Playtest creation failed.");
    } finally {
      setLoading(false);
    }
  };

  const addHotspot = () => {
    const id = createId("hotspot");
    setEditorDoc((current) => ({
      ...current,
      room: {
        ...current.room,
        hotspots: [
          ...current.room.hotspots,
          {
            id,
            name: `Hotspot ${current.room.hotspots.length + 1}`,
            x: 40,
            y: 40,
            width: 100,
            height: 48,
            color: "#facc15",
            visible: true,
            available: true,
            locked: false,
            interactive: true,
            hitArea: "rect",
          },
        ],
      },
    }));
    setSelectedHotspotId(id);
  };

  const updateHotspot = (patch: Partial<NonNullable<typeof selectedHotspot>>) => {
    if (!selectedHotspotId) {
      return;
    }

    setEditorDoc((current) => ({
      ...current,
      room: {
        ...current.room,
        hotspots: current.room.hotspots.map((entry) =>
          entry.id === selectedHotspotId ? { ...entry, ...patch } : entry
        ),
      },
    }));
  };

  const addNode = (family: TriggerGraphNode["family"]) => {
    const nodeId = createId(family);
    setEditorDoc((current) => ({
      ...current,
      triggerGraph: {
        ...current.triggerGraph,
        nodes: [
          ...current.triggerGraph.nodes,
          {
            nodeId,
            family,
            type: family === "condition" ? "actionTypeEquals" : family === "combinator" ? "allTrue" : "emitMessage",
            config: family === "condition" ? { expectedActionType: "inspect" } : family === "effect" ? { message: "Triggered" } : {},
            policy: {
              mode: "one-shot",
              keyWindowSeconds: 30,
            },
          },
        ],
      },
    }));
    setSelectedNodeId(nodeId);
  };

  const removeNode = (nodeId: string) => {
    setEditorDoc((current) => ({
      ...current,
      triggerGraph: {
        ...current.triggerGraph,
        nodes: current.triggerGraph.nodes.filter((x) => x.nodeId !== nodeId),
        edges: current.triggerGraph.edges.filter((x) => x.fromNodeId !== nodeId && x.toNodeId !== nodeId),
      },
    }));
  };

  const addEdge = (fromNodeId: string, toNodeId: string) => {
    if (!fromNodeId || !toNodeId || fromNodeId === toNodeId) {
      return;
    }

    setEditorDoc((current) => {
      if (current.triggerGraph.edges.some((x) => x.fromNodeId === fromNodeId && x.toNodeId === toNodeId)) {
        return current;
      }

      return {
        ...current,
        triggerGraph: {
          ...current.triggerGraph,
          edges: [...current.triggerGraph.edges, { fromNodeId, toNodeId }],
        },
      };
    });
  };

  return (
    <main className="mx-auto flex max-w-7xl flex-col gap-4 p-4 text-slate-100">
      <AuthPanel
        title="Creator Access"
        subtitle="Sign in with a Creator or Admin account to load, validate, save, and playtest rooms."
      />
      <header className="rounded border border-slate-700 bg-slate-900 p-4">
        <h1 className="text-2xl font-semibold">Room Editor + Trigger Builder (Day 6)</h1>
        <p className="mt-1 text-sm text-slate-300">Edit room layout, author triggers, validate, save immutable versions, and launch playtest.</p>
      </header>

      <section className="grid gap-3 rounded border border-slate-700 bg-slate-900 p-4 lg:grid-cols-[2fr_1fr_1fr_1fr]">
        <input value={roomId} onChange={(e) => setRoomId(e.target.value)} placeholder="Room UUID" className="rounded border border-slate-600 bg-slate-800 px-3 py-2" />
        <button onClick={loadRoom} disabled={loading} className="rounded bg-slate-700 px-3 py-2 text-sm">Load</button>
        <button onClick={validateDocument} disabled={loading} className="rounded bg-amber-600 px-3 py-2 text-sm text-white">Validate</button>
        <button onClick={saveDocument} disabled={loading} className="rounded bg-emerald-600 px-3 py-2 text-sm text-white">Save Version</button>
      </section>

      <section className="grid gap-4 lg:grid-cols-[2fr_1fr]">
        <div className="space-y-3 rounded border border-slate-700 bg-slate-900 p-4">
          <div className="flex items-center justify-between">
            <h2 className="text-lg font-semibold">Visual Editor</h2>
            <button onClick={addHotspot} className="rounded bg-blue-600 px-3 py-1 text-sm text-white">Add Hotspot</button>
          </div>
          <RoomCanvas
            room={editorDoc.room}
            onInspect={(id) => setSelectedHotspotId(id)}
            onPickup={(id) => setSelectedHotspotId(id)}
            onHotspotFocus={(id) => setSelectedHotspotId(id)}
          />

          {selectedHotspot && (
            <div className="grid gap-2 rounded border border-slate-700 bg-slate-800 p-3 md:grid-cols-2">
              <input value={selectedHotspot.name} onChange={(e) => updateHotspot({ name: e.target.value })} className="rounded border border-slate-600 bg-slate-900 px-2 py-1" />
              <input type="color" value={selectedHotspot.color} onChange={(e) => updateHotspot({ color: e.target.value })} className="h-9 rounded border border-slate-600 bg-slate-900 px-2 py-1" />
              <input type="number" value={selectedHotspot.x} onChange={(e) => updateHotspot({ x: Number(e.target.value) })} className="rounded border border-slate-600 bg-slate-900 px-2 py-1" />
              <input type="number" value={selectedHotspot.y} onChange={(e) => updateHotspot({ y: Number(e.target.value) })} className="rounded border border-slate-600 bg-slate-900 px-2 py-1" />
              <input type="number" value={selectedHotspot.width} onChange={(e) => updateHotspot({ width: Number(e.target.value) })} className="rounded border border-slate-600 bg-slate-900 px-2 py-1" />
              <input type="number" value={selectedHotspot.height} onChange={(e) => updateHotspot({ height: Number(e.target.value) })} className="rounded border border-slate-600 bg-slate-900 px-2 py-1" />
            </div>
          )}
        </div>

        <aside className="space-y-3 rounded border border-slate-700 bg-slate-900 p-4">
          <h2 className="text-lg font-semibold">Trigger Builder</h2>
          <div className="flex flex-wrap gap-2">
            <button onClick={() => addNode("condition")} className="rounded bg-sky-700 px-2 py-1 text-xs">+ Condition</button>
            <button onClick={() => addNode("combinator")} className="rounded bg-violet-700 px-2 py-1 text-xs">+ Combinator</button>
            <button onClick={() => addNode("effect")} className="rounded bg-emerald-700 px-2 py-1 text-xs">+ Effect</button>
          </div>

          <div className="max-h-56 overflow-auto rounded border border-slate-700">
            {editorDoc.triggerGraph.nodes.map((node) => (
              <button
                key={node.nodeId}
                onClick={() => setSelectedNodeId(node.nodeId)}
                className={`flex w-full items-center justify-between border-b border-slate-800 px-2 py-2 text-left text-xs ${selectedNodeId === node.nodeId ? "bg-slate-700" : "bg-slate-900"}`}
              >
                <span>{node.nodeId}</span>
                <span className="text-slate-300">{node.family}/{node.type}</span>
              </button>
            ))}
          </div>

          {selectedNode && (
            <div className="space-y-2 rounded border border-slate-700 bg-slate-800 p-2">
              <input
                value={selectedNode.type}
                onChange={(e) => {
                  const value = e.target.value;
                  setEditorDoc((current) => ({
                    ...current,
                    triggerGraph: {
                      ...current.triggerGraph,
                      nodes: current.triggerGraph.nodes.map((entry) =>
                        entry.nodeId === selectedNode.nodeId ? { ...entry, type: value } : entry
                      ),
                    },
                  }));
                }}
                className="w-full rounded border border-slate-600 bg-slate-900 px-2 py-1 text-xs"
              />
              <button onClick={() => removeNode(selectedNode.nodeId)} className="rounded bg-red-700 px-2 py-1 text-xs text-white">Remove Node</button>
            </div>
          )}

          <div className="space-y-1 rounded border border-slate-700 bg-slate-800 p-2">
            <p className="text-xs text-slate-300">Quick Edge</p>
            <div className="flex gap-2">
              <select
                value={edgeFromNodeId}
                onChange={(e) => setEdgeFromNodeId(e.target.value)}
                className="w-full rounded border border-slate-600 bg-slate-900 px-2 py-1 text-xs"
              >
                <option value="">from</option>
                {editorDoc.triggerGraph.nodes.map((node) => <option key={`from-${node.nodeId}`} value={node.nodeId}>{node.nodeId}</option>)}
              </select>
              <select
                value={edgeToNodeId}
                onChange={(e) => setEdgeToNodeId(e.target.value)}
                className="w-full rounded border border-slate-600 bg-slate-900 px-2 py-1 text-xs"
              >
                <option value="">to</option>
                {editorDoc.triggerGraph.nodes.map((node) => <option key={`to-${node.nodeId}`} value={node.nodeId}>{node.nodeId}</option>)}
              </select>
            </div>
            <button
              disabled={!edgeFromNodeId || !edgeToNodeId || edgeFromNodeId === edgeToNodeId}
              onClick={() => {
                addEdge(edgeFromNodeId, edgeToNodeId);
              }}
              className="rounded bg-slate-600 px-2 py-1 text-xs"
            >
              Add Edge
            </button>
            <p className="text-xs text-slate-400">Edges: {editorDoc.triggerGraph.edges.length}</p>
          </div>

          <button onClick={createPlaytest} disabled={loading} className="w-full rounded bg-teal-600 px-3 py-2 text-sm text-white">Create Playtest Session</button>
        </aside>
      </section>

      {status && <p className="rounded border border-slate-700 bg-slate-900 p-2 text-sm text-slate-200">{status}</p>}
      {issues.length > 0 && (
        <section className="rounded border border-red-700 bg-red-950 p-3">
          <h3 className="mb-2 text-sm font-semibold text-red-200">Validation Issues</h3>
          <ul className="space-y-1 text-xs text-red-100">
            {issues.map((issue, index) => (
              <li key={`${issue.code}-${index}`}>[{issue.path}] {issue.message}</li>
            ))}
          </ul>
        </section>
      )}
    </main>
  );
};

export default EditorPage;
