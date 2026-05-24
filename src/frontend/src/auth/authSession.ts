import { useSyncExternalStore } from "react";
import { StoredAuthSession } from "../types/auth";

const authStorageKey = "escape-room.auth-session";

let currentSession: StoredAuthSession | null = readStoredSession();
const listeners = new Set<() => void>();
let storageListenerAttached = false;

function readStoredSession(): StoredAuthSession | null {
  if (typeof window === "undefined") {
    return null;
  }

  const raw = window.localStorage.getItem(authStorageKey);
  if (!raw) {
    return null;
  }

  try {
    return JSON.parse(raw) as StoredAuthSession;
  } catch {
    window.localStorage.removeItem(authStorageKey);
    return null;
  }
}

function emitChange() {
  listeners.forEach((listener) => listener());
}

function ensureStorageListener() {
  if (storageListenerAttached || typeof window === "undefined") {
    return;
  }

  window.addEventListener("storage", (event) => {
    if (event.key !== authStorageKey) {
      return;
    }

    currentSession = readStoredSession();
    emitChange();
  });

  storageListenerAttached = true;
}

export function getAuthSession(): StoredAuthSession | null {
  return currentSession;
}

export function setAuthSession(session: StoredAuthSession) {
  currentSession = session;
  if (typeof window !== "undefined") {
    window.localStorage.setItem(authStorageKey, JSON.stringify(session));
  }
  emitChange();
}

export function clearAuthSession() {
  currentSession = null;
  if (typeof window !== "undefined") {
    window.localStorage.removeItem(authStorageKey);
  }
  emitChange();
}

function subscribe(listener: () => void) {
  ensureStorageListener();
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

const getSnapshot = () => currentSession;

export function useAuthSession() {
  const session = useSyncExternalStore(subscribe, getSnapshot, () => null);

  return {
    session,
    accessToken: session?.accessToken ?? "",
    refreshToken: session?.refreshToken ?? "",
    user: session?.user ?? null,
    isAuthenticated: Boolean(session?.accessToken),
    expiresAtUtc: session?.accessTokenExpiresAtUtc ?? null,
  };
}
