import { parseActionError } from "./actionError";

describe("parseActionError", () => {
  it("parses structured hub rate-limit payload", () => {
    const error = new Error(
      JSON.stringify({
        code: "rate_limited",
        message: "Action rate limited.",
        retryAfterMs: 1400,
        policyName: "player-action-default",
      })
    );

    const parsed = parseActionError(error, "inspect:desk-note");

    expect(parsed.source).toBe("server-rate-limit");
    expect(parsed.retryAfterMs).toBe(1400);
    expect(parsed.policyName).toBe("player-action-default");
    expect(parsed.message).toBe("Action rate limited.");
  });
});
