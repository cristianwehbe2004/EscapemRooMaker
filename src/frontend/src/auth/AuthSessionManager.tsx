import React, { useEffect } from "react";
import { refreshAuthSession } from "./authApi";
import { clearAuthSession, getAuthSession, useAuthSession } from "./authSession";

const refreshLeadMs = 60_000;

let refreshTimerId: number | null = null;
let refreshInFlight: Promise<void> | null = null;

const clearRefreshTimer = () => {
  if (refreshTimerId !== null && typeof window !== "undefined") {
    window.clearTimeout(refreshTimerId);
  }
  refreshTimerId = null;
};

const runRefresh = async (expectedRefreshToken: string) => {
  if (refreshInFlight) {
    return refreshInFlight;
  }

  refreshInFlight = (async () => {
    const current = getAuthSession();
    if (!current || current.refreshToken !== expectedRefreshToken) {
      return;
    }

    try {
      await refreshAuthSession(expectedRefreshToken);
    } catch {
      const latest = getAuthSession();
      if (latest?.refreshToken === expectedRefreshToken) {
        clearAuthSession();
      }
    } finally {
      refreshInFlight = null;
    }
  })();

  return refreshInFlight;
};

const scheduleRefresh = (expiresAtUtc: string, refreshToken: string) => {
  clearRefreshTimer();

  if (typeof window === "undefined") {
    return;
  }

  const expiresAt = new Date(expiresAtUtc).getTime();
  if (Number.isNaN(expiresAt)) {
    return;
  }

  const delayMs = Math.max(0, expiresAt - Date.now() - refreshLeadMs);
  refreshTimerId = window.setTimeout(() => {
    void runRefresh(refreshToken);
  }, delayMs);
};

const AuthSessionManager: React.FC = () => {
  const { isAuthenticated, expiresAtUtc, refreshToken } = useAuthSession();

  useEffect(() => {
    if (!isAuthenticated || !expiresAtUtc || !refreshToken) {
      clearRefreshTimer();
      return;
    }

    scheduleRefresh(expiresAtUtc, refreshToken);

    return () => {
      clearRefreshTimer();
    };
  }, [expiresAtUtc, isAuthenticated, refreshToken]);

  return null;
};

export default AuthSessionManager;
