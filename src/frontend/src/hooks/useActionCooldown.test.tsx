import { act, renderHook } from "@testing-library/react";
import { useActionCooldown } from "./useActionCooldown";

describe("useActionCooldown", () => {
  it("blocks duplicate action keys inside the cooldown window", async () => {
    const { result } = renderHook(() => useActionCooldown(1000));
    const action = jest.fn().mockResolvedValue(undefined);

    await act(async () => {
      const first = await result.current.runWithCooldown("inspect:desk-note", action);
      expect(first).toEqual({ allowed: true, remainingMs: 0 });
    });

    await act(async () => {
      const second = await result.current.runWithCooldown("inspect:desk-note", action);
      expect(second.allowed).toBe(false);
      expect(second.remainingMs).toBeGreaterThan(0);
    });

    expect(action).toHaveBeenCalledTimes(1);
  });
});
