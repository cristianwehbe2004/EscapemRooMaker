import React from "react";
import { act, render, screen, waitFor } from "@testing-library/react";
import AuthSessionManager from "./AuthSessionManager";
import { clearAuthSession, setAuthSession, useAuthSession } from "./authSession";

const SessionProbe: React.FC = () => {
  const { user, accessToken } = useAuthSession();
  return <div>{user ? `${user.username}:${accessToken}` : "signed-out"}</div>;
};

describe("AuthSessionManager", () => {
  beforeEach(() => {
    jest.useFakeTimers();
    act(() => {
      clearAuthSession();
    });
    window.localStorage.clear();
    jest.clearAllMocks();
  });

  afterEach(() => {
    jest.useRealTimers();
    act(() => {
      clearAuthSession();
    });
    window.localStorage.clear();
  });

  it("refreshes an expiring session automatically", async () => {
    const fetchMock = jest.fn().mockResolvedValue({
      ok: true,
      json: async () => ({
        accessToken: "token-2",
        refreshToken: "refresh-2",
        accessTokenExpiresAtUtc: new Date(Date.now() + 30 * 60 * 1000).toISOString(),
        user: {
          id: "user-1",
          username: "player1",
          email: "player1@escaperoom.local",
          role: "Player",
        },
      }),
      headers: new Headers({ "content-type": "application/json" }),
    } as Response);
    global.fetch = fetchMock as unknown as typeof fetch;

    setAuthSession({
      accessToken: "token-1",
      refreshToken: "refresh-1",
      accessTokenExpiresAtUtc: new Date(Date.now() + 59_000).toISOString(),
      user: {
        id: "user-1",
        username: "player1",
        email: "player1@escaperoom.local",
        role: "Player",
      },
    });

    render(
      <>
        <AuthSessionManager />
        <SessionProbe />
      </>
    );

    expect(screen.getByText("player1:token-1")).toBeInTheDocument();

    await act(async () => {
      jest.advanceTimersByTime(60_000);
    });

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledWith(
        "http://localhost:5130/api/auth/refresh",
        expect.objectContaining({
          method: "POST",
          body: JSON.stringify({ refreshToken: "refresh-1" }),
        })
      );
    });

    await waitFor(() => {
      expect(screen.getByText("player1:token-2")).toBeInTheDocument();
    });
  });

  it("clears auth when refresh fails", async () => {
    const fetchMock = jest.fn().mockResolvedValue({
      ok: false,
      json: async () => ({ message: "Invalid refresh token." }),
      headers: new Headers({ "content-type": "application/json" }),
    } as Response);
    global.fetch = fetchMock as unknown as typeof fetch;

    setAuthSession({
      accessToken: "token-1",
      refreshToken: "refresh-1",
      accessTokenExpiresAtUtc: new Date(Date.now() + 59_000).toISOString(),
      user: {
        id: "user-1",
        username: "player1",
        email: "player1@escaperoom.local",
        role: "Player",
      },
    });

    render(
      <>
        <AuthSessionManager />
        <SessionProbe />
      </>
    );

    await act(async () => {
      jest.advanceTimersByTime(60_000);
    });

    await waitFor(() => {
      expect(screen.getByText("signed-out")).toBeInTheDocument();
    });
  });
});
