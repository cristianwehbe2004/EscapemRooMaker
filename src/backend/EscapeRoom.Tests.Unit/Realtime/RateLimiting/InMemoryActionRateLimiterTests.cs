using EscapeRoom.Application.Realtime.Contracts;
using EscapeRoom.Realtime.RateLimiting;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace EscapeRoom.Tests.Unit.Realtime.RateLimiting;

public class InMemoryActionRateLimiterTests
{
    [Fact]
    public void Evaluate_ShouldAllowFirstAction_ThenDenyWithinCooldown()
    {
        var limiter = new InMemoryActionRateLimiter(Options.Create(new ActionRateLimitOptions
        {
            CooldownMs = 300,
            PolicyName = "test-policy"
        }));

        var sessionId = Guid.NewGuid();
        var action = new PlayerActionEnvelope
        {
            ActionType = "inspect",
            Actor = "player-1",
            Target = "desk-note",
            Payload = new Dictionary<string, object?>()
        };

        var first = limiter.Evaluate(sessionId, action);
        var second = limiter.Evaluate(sessionId, action);

        first.Allowed.Should().BeTrue();
        second.Allowed.Should().BeFalse();
        second.PolicyName.Should().Be("test-policy");
        second.RetryAfterMs.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Evaluate_ShouldUseDifferentKeysForDifferentTargets()
    {
        var limiter = new InMemoryActionRateLimiter(Options.Create(new ActionRateLimitOptions
        {
            CooldownMs = 300,
            PolicyName = "test-policy"
        }));

        var sessionId = Guid.NewGuid();
        var first = limiter.Evaluate(sessionId, new PlayerActionEnvelope
        {
            ActionType = "inspect",
            Actor = "player-1",
            Target = "desk-note",
            Payload = new Dictionary<string, object?>()
        });

        var second = limiter.Evaluate(sessionId, new PlayerActionEnvelope
        {
            ActionType = "inspect",
            Actor = "player-1",
            Target = "door",
            Payload = new Dictionary<string, object?>()
        });

        first.Allowed.Should().BeTrue();
        second.Allowed.Should().BeTrue();
    }
}
