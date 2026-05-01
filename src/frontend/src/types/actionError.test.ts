import { parseActionError } from "./actionError";

describe("parseActionError", () => {
  it("parses structured hub rate-limit payload", () => {
    const error = new Error(
      JSON.stringify({
        code: "rate_limited",
        message: "Action rate limited.",
        retryAfterMs: 1400,
        policyName: "player-action-default",
        policyScope: "player",
        actionKey: "player:inspect:desk-note",
      })
    );

    const parsed = parseActionError(error, "inspect:desk-note");

    expect(parsed.source).toBe("server-rate-limit");
    expect(parsed.retryAfterMs).toBe(1400);
    expect(parsed.policyName).toBe("player-action-default");
    expect(parsed.policyScope).toBe("player");
    expect(parsed.actionKey).toBe("player:inspect:desk-note");
    expect(parsed.message).toBe("Action rate limited.");
  });

  it("falls back to generic server parsing for unstructured errors", () => {
    const parsed = parseActionError(new Error("Action failed unexpectedly"));

    expect(parsed.source).toBe("server");
    expect(parsed.message).toBe("Action failed unexpectedly");
  });
});
