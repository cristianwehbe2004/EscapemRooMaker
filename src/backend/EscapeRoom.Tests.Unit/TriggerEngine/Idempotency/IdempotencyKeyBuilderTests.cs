using EscapeRoom.Application.Realtime.Contracts;
using EscapeRoom.Application.Triggering.Contracts;
using EscapeRoom.TriggerEngine.Idempotency;
using FluentAssertions;

namespace EscapeRoom.Tests.Unit.TriggerEngine.Idempotency;

public class IdempotencyKeyBuilderTests
{
    private readonly IdempotencyKeyBuilder _builder = new();

    [Fact]
    public void Build_ShouldUseWindowBucketForRepeatablePolicy()
    {
        var key = _builder.Build(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new TriggerNodeDefinition
            {
                NodeId = "effect1",
                Policy = new EffectPolicyDefinition
                {
                    Mode = "repeatable",
                    KeyWindowSeconds = 30
                }
            },
            new PlayerActionEnvelope
            {
                ClientActionId = "client-1",
                TimestampUtc = DateTime.UnixEpoch.AddSeconds(90)
            });

        // 90 seconds / 30 window = bucket 3
        key.Should().EndWith(":3");
    }

    [Fact]
    public void Build_ShouldNotUseWindowForOneShotPolicy()
    {
        var key = _builder.Build(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new TriggerNodeDefinition
            {
                NodeId = "effect1",
                Policy = new EffectPolicyDefinition
                {
                    Mode = "one-shot"
                }
            },
            new PlayerActionEnvelope
            {
                ClientActionId = "client-1",
                TimestampUtc = DateTime.UnixEpoch.AddSeconds(90)
            });

        key.Should().Be("idempotency:11111111-1111-1111-1111-111111111111:effect1:client-1");
    }

    [Fact]
    public void Build_ShouldDefaultToOneShotMode()
    {
        var key = _builder.Build(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new TriggerNodeDefinition
            {
                NodeId = "effect1",
                Policy = new EffectPolicyDefinition
                {
                    Mode = ""  // Empty mode should default to one-shot
                }
            },
            new PlayerActionEnvelope
            {
                ClientActionId = "client-1",
                TimestampUtc = DateTime.UnixEpoch.AddSeconds(90)
            });

        key.Should().Be("idempotency:11111111-1111-1111-1111-111111111111:effect1:client-1");
    }

    [Fact]
    public void Build_ShouldDefaultToOneShotModeWhenNull()
    {
        var key = _builder.Build(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new TriggerNodeDefinition
            {
                NodeId = "effect1",
                Policy = new EffectPolicyDefinition
                {
                    Mode = null  // Null mode should default to one-shot
                }
            },
            new PlayerActionEnvelope
            {
                ClientActionId = "client-1",
                TimestampUtc = DateTime.UnixEpoch.AddSeconds(90)
            });

        key.Should().Be("idempotency:11111111-1111-1111-1111-111111111111:effect1:client-1");
    }

    [Fact]
    public void Build_ShouldHandleDifferentTimeWindows()
    {
        // Window of 60 seconds, at 120 seconds = bucket 2
        var key = _builder.Build(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new TriggerNodeDefinition
            {
                NodeId = "effect1",
                Policy = new EffectPolicyDefinition
                {
                    Mode = "repeatable",
                    KeyWindowSeconds = 60
                }
            },
            new PlayerActionEnvelope
            {
                ClientActionId = "client-1",
                TimestampUtc = DateTime.UnixEpoch.AddSeconds(120)
            });

        key.Should().EndWith(":2");
    }

    [Fact]
    public void Build_ShouldUseMinimumWindowOf1Second()
    {
        // Window of 0 should be treated as 1
        var key = _builder.Build(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new TriggerNodeDefinition
            {
                NodeId = "effect1",
                Policy = new EffectPolicyDefinition
                {
                    Mode = "repeatable",
                    KeyWindowSeconds = 0
                }
            },
            new PlayerActionEnvelope
            {
                ClientActionId = "client-1",
                TimestampUtc = DateTime.UnixEpoch.AddSeconds(90)
            });

        key.Should().EndWith(":90");
    }

    [Fact]
    public void ResolveTtl_ShouldReturn24HoursForOneShot()
    {
        var ttl = _builder.ResolveTtl(new TriggerNodeDefinition
        {
            NodeId = "effect1",
            Policy = new EffectPolicyDefinition
            {
                Mode = "one-shot"
            }
        });

        ttl.Should().Be(TimeSpan.FromHours(24));
    }

    [Fact]
    public void ResolveTtl_ShouldReturnWindowSizeForRepeatable()
    {
        var ttl = _builder.ResolveTtl(new TriggerNodeDefinition
        {
            NodeId = "effect1",
            Policy = new EffectPolicyDefinition
            {
                Mode = "repeatable",
                KeyWindowSeconds = 30
            }
        });

        ttl.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void ResolveTtl_ShouldDefaultTo24Hours()
    {
        var ttl = _builder.ResolveTtl(new TriggerNodeDefinition
        {
            NodeId = "effect1",
            Policy = new EffectPolicyDefinition
            {
                Mode = ""  // Empty mode defaults to one-shot with 24h TTL
            }
        });

        ttl.Should().Be(TimeSpan.FromHours(24));
    }

    [Fact]
    public void ResolveTtl_ShouldUseDefaultWindowForRepeatableWithoutWindow()
    {
        // Repeatable with no KeyWindowSeconds should default to 30 seconds
        var ttl = _builder.ResolveTtl(new TriggerNodeDefinition
        {
            NodeId = "effect1",
            Policy = new EffectPolicyDefinition
            {
                Mode = "repeatable"
            }
        });

        ttl.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Build_ShouldIncludeAllComponentsInKey()
    {
        var sessionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var key = _builder.Build(
            sessionId,
            new TriggerNodeDefinition
            {
                NodeId = "myEffect",
                Policy = new EffectPolicyDefinition { Mode = "one-shot" }
            },
            new PlayerActionEnvelope
            {
                ClientActionId = "action-123",
                TimestampUtc = DateTime.UnixEpoch
            });

        key.Should().StartWith("idempotency:");
        key.Should().Contain(sessionId.ToString());
        key.Should().Contain("myEffect");
        key.Should().Contain("action-123");
    }
}