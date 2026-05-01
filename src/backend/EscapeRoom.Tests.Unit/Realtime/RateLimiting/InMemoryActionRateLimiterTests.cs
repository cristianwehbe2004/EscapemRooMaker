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
            PolicyName = "test-policy",
            Gm = new ActionRateLimitPolicyOptions
            {
                Enabled = false,
                CooldownMs = 0,
                PolicyName = "gm-action-bypass"
            }
        }));

        var sessionId = Guid.NewGuid();
        var action = new PlayerActionEnvelope
        {
            ActionType = "inspect",
            Actor = "player-1",
            Target = "desk-note",
            Payload = new Dictionary<string, object?>()
        };

        var first = limiter.Evaluate(sessionId, action, new ActionRateLimitContext
        {
            PolicyScope = "player",
            ActorRole = "player"
        });
        var second = limiter.Evaluate(sessionId, action, new ActionRateLimitContext
        {
            PolicyScope = "player",
            ActorRole = "player"
        });

        first.Allowed.Should().BeTrue();
        second.Allowed.Should().BeFalse();
        second.PolicyName.Should().Be("test-policy");
        second.RetryAfterMs.Should().BeGreaterThan(0);
        second.PolicyScope.Should().Be("player");
        second.ActionKey.Should().Contain("inspect");
    }

    [Fact]
    public void Evaluate_ShouldUseDifferentKeysForDifferentTargets()
    {
        var limiter = new InMemoryActionRateLimiter(Options.Create(new ActionRateLimitOptions
        {
            CooldownMs = 300,
            PolicyName = "test-policy",
            Gm = new ActionRateLimitPolicyOptions
            {
                Enabled = false,
                CooldownMs = 0,
                PolicyName = "gm-action-bypass"
            }
        }));

        var sessionId = Guid.NewGuid();
        var first = limiter.Evaluate(sessionId, new PlayerActionEnvelope
        {
            ActionType = "inspect",
            Actor = "player-1",
            Target = "desk-note",
            Payload = new Dictionary<string, object?>()
        }, new ActionRateLimitContext
        {
            PolicyScope = "player",
            ActorRole = "player"
        });

        var second = limiter.Evaluate(sessionId, new PlayerActionEnvelope
        {
            ActionType = "inspect",
            Actor = "player-1",
            Target = "door",
            Payload = new Dictionary<string, object?>()
        }, new ActionRateLimitContext
        {
            PolicyScope = "player",
            ActorRole = "player"
        });

        first.Allowed.Should().BeTrue();
        second.Allowed.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_ShouldBypassGmPolicyByDefault()
    {
        var limiter = new InMemoryActionRateLimiter(Options.Create(new ActionRateLimitOptions
        {
            CooldownMs = 900,
            PolicyName = "player-action-default",
            Gm = new ActionRateLimitPolicyOptions
            {
                Enabled = false,
                CooldownMs = 0,
                PolicyName = "gm-action-bypass"
            }
        }));

        var sessionId = Guid.NewGuid();
        var action = new PlayerActionEnvelope
        {
            ActionType = "gm.hint",
            Actor = "gm-1",
            Target = "team-a",
            Payload = new Dictionary<string, object?>()
        };

        var first = limiter.Evaluate(sessionId, action, new ActionRateLimitContext
        {
            PolicyScope = "gm",
            ActorRole = "gm"
        });
        var second = limiter.Evaluate(sessionId, action, new ActionRateLimitContext
        {
            PolicyScope = "gm",
            ActorRole = "gm"
        });

        first.Allowed.Should().BeTrue();
        second.Allowed.Should().BeTrue();
        second.PolicyName.Should().Be("gm-action-bypass");
        second.PolicyScope.Should().Be("gm");
    }
}
