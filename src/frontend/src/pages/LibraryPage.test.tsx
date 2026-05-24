import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { clearAuthSession } from "../auth/authSession";
import LibraryPage from "./LibraryPage";

describe("LibraryPage", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    clearAuthSession();
    window.localStorage.clear();
  });

  it("renders library rooms and supports sort changes", async () => {
    const fetchMock = jest.fn()
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({
          items: [
            {
              roomId: "room-1",
              name: "Vault Alpha",
              description: "desc",
              createdAtUtc: new Date().toISOString(),
              ratingCount: 2,
              averageRating: 4.5,
              viewerRating: null,
              difficulty: "medium",
              estimatedMinutes: 5,
            },
          ],
          page: 1,
          pageSize: 20,
          total: 1,
        }),
      } as Response)
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({
          items: [],
          page: 1,
          pageSize: 20,
          total: 0,
        }),
      } as Response);
    global.fetch = fetchMock as unknown as typeof fetch;

    render(<LibraryPage />);

    await waitFor(() => {
      expect(screen.getByText("Vault Alpha")).toBeInTheDocument();
    });

    expect(screen.getByText("medium")).toBeInTheDocument();
    expect(screen.getByText("Estimated: 5 minutes")).toBeInTheDocument();

    fireEvent.change(screen.getByDisplayValue("Newest"), { target: { value: "name" } });

    await waitFor(() => {
      expect(fetchMock).toHaveBeenCalledTimes(2);
    });
  });

  it("submits rating and updates aggregate display", async () => {
    const createdAt = new Date().toISOString();
    const fetchMock = jest.fn()
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({
          items: [
            {
              roomId: "room-1",
              name: "Vault Alpha",
              description: "desc",
              createdAtUtc: createdAt,
              ratingCount: 2,
              averageRating: 4.5,
              viewerRating: null,
              difficulty: "hard",
            },
          ],
          page: 1,
          pageSize: 20,
          total: 1,
        }),
      } as Response)
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({
          accessToken: "token-123",
          refreshToken: "refresh-123",
          accessTokenExpiresAtUtc: new Date(Date.now() + 30 * 60 * 1000).toISOString(),
          user: {
            id: "user-1",
            username: "player1",
            email: "player1@escaperoom.local",
            role: "Player",
          },
        }),
        headers: new Headers({ "content-type": "application/json" }),
      } as Response)
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({
          items: [
            {
              roomId: "room-1",
              name: "Vault Alpha",
              description: "desc",
              createdAtUtc: createdAt,
              ratingCount: 2,
              averageRating: 4.5,
              viewerRating: null,
              difficulty: "hard",
            },
          ],
          page: 1,
          pageSize: 20,
          total: 1,
        }),
      } as Response)
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({
          roomId: "room-1",
          score: 5,
          ratingCount: 3,
          averageRating: 4.67,
        }),
      } as Response);
    global.fetch = fetchMock as unknown as typeof fetch;

    render(<LibraryPage />);

    await waitFor(() => {
      expect(screen.getByText("Vault Alpha")).toBeInTheDocument();
    });

    fireEvent.change(screen.getByPlaceholderText("Email"), { target: { value: "player1@escaperoom.local" } });
    fireEvent.change(screen.getByPlaceholderText("Password"), { target: { value: "Player123!" } });
    fireEvent.click(screen.getAllByRole("button", { name: "Sign In" })[1]);
    await waitFor(() => {
      expect(screen.getByText(/signed in as player1/i)).toBeInTheDocument();
    });
    fireEvent.click(screen.getByRole("button", { name: "Rate 5" }));

    await waitFor(() => {
      expect(screen.getByText(/your rating: 5/i)).toBeInTheDocument();
      expect(screen.getByText(/rating: 4.67 \(3 votes\)/i)).toBeInTheDocument();
    });
  });
});
