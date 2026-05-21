import React, { useEffect, useMemo, useState } from "react";
import { LibraryRoomListItemDto, LibraryRoomsResponse, UpsertRoomRatingResponse } from "../types/library";

const apiBaseUrl = process.env.REACT_APP_API_BASE_URL ?? "http://localhost:5130";
const pageSize = 20;

const getDifficultyChipClass = (difficulty?: string | null): string => {
  const value = difficulty?.toLowerCase();
  if (value === "hard") {
    return "bg-rose-900/70 text-rose-200";
  }

  if (value === "medium") {
    return "bg-amber-900/70 text-amber-200";
  }

  if (value === "easy") {
    return "bg-emerald-900/70 text-emerald-200";
  }

  return "bg-slate-700 text-slate-200";
};

const LibraryPage: React.FC = () => {
  const [accessToken, setAccessToken] = useState("");
  const [queryInput, setQueryInput] = useState("");
  const [debouncedQuery, setDebouncedQuery] = useState("");
  const [sort, setSort] = useState<"newest" | "name" | "rating">("newest");
  const [rooms, setRooms] = useState<LibraryRoomListItemDto[]>([]);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [ratingBusy, setRatingBusy] = useState<string | null>(null);

  useEffect(() => {
    const timer = window.setTimeout(() => setDebouncedQuery(queryInput.trim()), 300);
    return () => window.clearTimeout(timer);
  }, [queryInput]);

  const loadRooms = async () => {
    setLoading(true);
    setError(null);
    try {
      const params = new URLSearchParams();
      if (debouncedQuery) {
        params.set("q", debouncedQuery);
      }
      params.set("sort", sort);
      params.set("page", page.toString());
      params.set("pageSize", pageSize.toString());

      const response = await fetch(`${apiBaseUrl}/api/library/rooms?${params.toString()}`, {
        headers: accessToken ? { Authorization: `Bearer ${accessToken}` } : undefined,
      });
      if (!response.ok) {
        throw new Error(`Failed to load library (${response.status})`);
      }

      const result = (await response.json()) as LibraryRoomsResponse;
      setRooms(result.items ?? []);
      setTotal(result.total ?? 0);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load library");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadRooms();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [debouncedQuery, sort, page, accessToken]);

  const totalPages = useMemo(() => Math.max(1, Math.ceil(total / pageSize)), [total]);

  const setRating = async (roomId: string, score: number) => {
    if (!accessToken.trim()) {
      setError("Provide a bearer token to rate rooms.");
      return;
    }

    setRatingBusy(roomId);
    setError(null);
    try {
      const response = await fetch(`${apiBaseUrl}/api/library/rooms/${roomId}/rating`, {
        method: "PUT",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${accessToken}`,
        },
        body: JSON.stringify({ score }),
      });
      if (!response.ok) {
        const text = await response.text();
        throw new Error(text || `Failed to submit rating (${response.status})`);
      }

      const updated = (await response.json()) as UpsertRoomRatingResponse;
      setRooms((current) =>
        current.map((room) =>
          room.roomId === roomId
            ? {
                ...room,
                viewerRating: updated.score,
                ratingCount: updated.ratingCount,
                averageRating: updated.averageRating,
              }
            : room
        )
      );
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to submit rating");
    } finally {
      setRatingBusy(null);
    }
  };

  return (
    <main className="mx-auto max-w-6xl p-6 text-slate-100">
      <h1 className="text-2xl font-semibold">Public Library</h1>
      <p className="mt-1 text-sm text-slate-300">Browse published rooms, search, sort, and rate.</p>

      <section className="mt-4 grid gap-3 rounded border border-slate-700 bg-slate-900 p-4 md:grid-cols-[2fr_1fr_2fr]">
        <input
          value={queryInput}
          onChange={(e) => {
            setPage(1);
            setQueryInput(e.target.value);
          }}
          placeholder="Search by room name or description"
          className="rounded border border-slate-600 bg-slate-800 px-3 py-2"
        />
        <select
          value={sort}
          onChange={(e) => {
            setPage(1);
            setSort(e.target.value as "newest" | "name" | "rating");
          }}
          className="rounded border border-slate-600 bg-slate-800 px-3 py-2"
        >
          <option value="newest">Newest</option>
          <option value="name">Name</option>
          <option value="rating">Rating</option>
        </select>
        <input
          value={accessToken}
          onChange={(e) => setAccessToken(e.target.value)}
          placeholder="Optional bearer token (required for rating)"
          className="rounded border border-slate-600 bg-slate-800 px-3 py-2"
        />
      </section>

      {loading && <p className="mt-4 text-sm text-slate-300">Loading rooms...</p>}
      {error && <p className="mt-4 rounded border border-red-700 bg-red-950 p-2 text-sm text-red-200">{error}</p>}
      {!loading && !error && rooms.length === 0 && (
        <p className="mt-4 rounded border border-slate-700 bg-slate-900 p-3 text-slate-300">No published rooms found.</p>
      )}

      <section className="mt-4 grid gap-3">
        {rooms.map((room) => (
          <article key={room.roomId} className="rounded border border-slate-700 bg-slate-900 p-4">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <h2 className="text-lg font-semibold">{room.name}</h2>
              <div className="flex items-center gap-2">
                {room.isFeatured && <span className="rounded bg-sky-800 px-2 py-1 text-[10px] uppercase tracking-wider text-sky-200">featured</span>}
                {room.difficulty && (
                  <span className={`rounded px-2 py-1 text-[10px] uppercase tracking-wider ${getDifficultyChipClass(room.difficulty)}`}>{room.difficulty}</span>
                )}
                <p className="text-xs text-slate-400">{new Date(room.createdAtUtc).toLocaleString()}</p>
              </div>
            </div>
            <p className="mt-2 text-sm text-slate-300">{room.description || "No description."}</p>
            {room.estimatedMinutes ? <p className="mt-1 text-xs text-slate-400">Estimated: {room.estimatedMinutes} minutes</p> : null}
            <p className="mt-2 text-sm text-slate-200">
              Rating: {room.averageRating.toFixed(2)} ({room.ratingCount} vote{room.ratingCount === 1 ? "" : "s"})
              {room.viewerRating ? ` • Your rating: ${room.viewerRating}` : ""}
            </p>
            <div className="mt-3 flex flex-wrap gap-2">
              {[1, 2, 3, 4, 5].map((score) => (
                <button
                  key={`${room.roomId}-rate-${score}`}
                  disabled={ratingBusy === room.roomId}
                  onClick={() => setRating(room.roomId, score)}
                  className={`rounded px-2 py-1 text-xs ${
                    room.viewerRating === score ? "bg-amber-500 text-slate-950" : "bg-slate-700 text-white"
                  }`}
                >
                  Rate {score}
                </button>
              ))}
            </div>
          </article>
        ))}
      </section>

      <section className="mt-4 flex items-center justify-between rounded border border-slate-700 bg-slate-900 p-3">
        <p className="text-sm text-slate-300">
          Page {page} / {totalPages} • {total} rooms
        </p>
        <div className="flex gap-2">
          <button
            disabled={page <= 1}
            onClick={() => setPage((x) => Math.max(1, x - 1))}
            className="rounded bg-slate-700 px-3 py-1 text-sm"
          >
            Previous
          </button>
          <button
            disabled={page >= totalPages}
            onClick={() => setPage((x) => Math.min(totalPages, x + 1))}
            className="rounded bg-slate-700 px-3 py-1 text-sm"
          >
            Next
          </button>
        </div>
      </section>
    </main>
  );
};

export default LibraryPage;
