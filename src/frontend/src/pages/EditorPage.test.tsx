import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import EditorPage from "./EditorPage";

const mockNavigate = jest.fn();

jest.mock(
  "react-router-dom",
  () => ({
    MemoryRouter: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
    useNavigate: () => mockNavigate,
  }),
  { virtual: true }
);

const MemoryRouter = ({ children }: { children: React.ReactNode }) => <div>{children}</div>;

jest.mock("../components/konva/RoomCanvas", () => {
  return function MockRoomCanvas() {
    return <div>Mock Room Canvas</div>;
  };
});

describe("EditorPage", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    global.fetch = jest.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ versionNumber: 3, issues: [] }),
      text: async () => "",
    } as Response);
  });

  it("renders and adds edge without runtime errors", () => {
    render(
      <MemoryRouter>
        <EditorPage />
      </MemoryRouter>
    );

    expect(screen.getByText(/room editor \+ trigger builder/i)).toBeInTheDocument();
    expect(screen.getByText("Edges: 0")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: /\+ condition/i }));
    fireEvent.click(screen.getByRole("button", { name: /\+ effect/i }));

    const fromSelect = screen.getAllByRole("combobox")[0];
    const toSelect = screen.getAllByRole("combobox")[1];

    const fromOption = Array.from(fromSelect.querySelectorAll("option")).find((x) =>
      x.value.startsWith("condition-")
    );
    const toOption = Array.from(toSelect.querySelectorAll("option")).find((x) =>
      x.value.startsWith("effect-")
    );

    expect(fromOption).toBeTruthy();
    expect(toOption).toBeTruthy();

    fireEvent.change(fromSelect, { target: { value: fromOption?.value } });
    fireEvent.change(toSelect, { target: { value: toOption?.value } });

    fireEvent.click(screen.getByRole("button", { name: /add edge/i }));
    expect(screen.getByText("Edges: 1")).toBeInTheDocument();
  });

  it("sends save payload with trigger graph after edge authoring", async () => {
    render(
      <MemoryRouter>
        <EditorPage />
      </MemoryRouter>
    );

    fireEvent.change(screen.getByPlaceholderText(/room uuid/i), { target: { value: "room-123" } });

    fireEvent.click(screen.getByRole("button", { name: /\+ condition/i }));
    fireEvent.click(screen.getByRole("button", { name: /\+ effect/i }));

    const fromSelect = screen.getAllByRole("combobox")[0];
    const toSelect = screen.getAllByRole("combobox")[1];
    const fromOption = Array.from(fromSelect.querySelectorAll("option")).find((x) =>
      x.value.startsWith("condition-")
    );
    const toOption = Array.from(toSelect.querySelectorAll("option")).find((x) =>
      x.value.startsWith("effect-")
    );

    fireEvent.change(fromSelect, { target: { value: fromOption?.value } });
    fireEvent.change(toSelect, { target: { value: toOption?.value } });
    fireEvent.click(screen.getByRole("button", { name: /add edge/i }));
    fireEvent.click(screen.getByRole("button", { name: /save version/i }));

    await waitFor(() => {
      expect(global.fetch).toHaveBeenCalledWith(
        "http://localhost:5130/api/creator/rooms/room-123",
        expect.objectContaining({ method: "PUT" })
      );
    });

    const [, request] = (global.fetch as jest.Mock).mock.calls.find(
      ([url]: [string]) => url === "http://localhost:5130/api/creator/rooms/room-123"
    );
    const body = JSON.parse(request.body as string);

    expect(body.document.triggerGraph.nodes.length).toBeGreaterThanOrEqual(2);
    expect(body.document.triggerGraph.edges.length).toBe(1);
  });
});
