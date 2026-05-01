using System.Text.Json;
using EscapeRoom.Application.Realtime.Contracts;
using EscapeRoom.Application.Triggering;
using StackExchange.Redis;

namespace EscapeRoom.Infrastructure.Triggering;

public class RedisSessionStateStore(IConnectionMultiplexer multiplexer) : ISessionStateStore
{
    private static readonly TimeSpan SnapshotTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan DiffTtl = TimeSpan.FromHours(6);
    private const int MaxDiffHistory = 500;

    public async Task<long> GetNextDiffSequenceAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var db = multiplexer.GetDatabase();
        var next = await db.StringIncrementAsync(DiffSeqKey(sessionId));
        await db.KeyExpireAsync(DiffSeqKey(sessionId), DiffTtl);
        return next;
    }

    public async Task SaveSnapshotAsync(SessionSnapshotEnvelope snapshot, CancellationToken cancellationToken = default)
    {
        var db = multiplexer.GetDatabase();
        await db.StringSetAsync(SnapshotKey(snapshot.SessionId), JsonSerializer.Serialize(snapshot, JsonOptions()), SnapshotTtl);
        await db.StringSetAsync(VersionKey(snapshot.SessionId), snapshot.SessionVersion, SnapshotTtl);
    }

    public async Task AppendDiffAsync(Guid sessionId, StateDiffEnvelope diff, CancellationToken cancellationToken = default)
    {
        var db = multiplexer.GetDatabase();
        var key = DiffsKey(sessionId);
        await db.ListRightPushAsync(key, JsonSerializer.Serialize(diff, JsonOptions()));
        await db.ListTrimAsync(key, -MaxDiffHistory, -1);
        await db.KeyExpireAsync(key, DiffTtl);
        await db.StringSetAsync(VersionKey(sessionId), diff.SessionVersion, SnapshotTtl);
    }

    public async Task<SessionSnapshotEnvelope?> GetSnapshotAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var db = multiplexer.GetDatabase();
        var raw = await db.StringGetAsync(SnapshotKey(sessionId));
        if (!raw.HasValue)
        {
            return null;
        }

        return JsonSerializer.Deserialize<SessionSnapshotEnvelope>(raw.ToString(), JsonOptions());
    }

    public async Task<IReadOnlyList<StateDiffEnvelope>> GetDiffsAfterVersionAsync(Guid sessionId, int lastKnownVersion, CancellationToken cancellationToken = default)
    {
        var db = multiplexer.GetDatabase();
        var values = await db.ListRangeAsync(DiffsKey(sessionId), 0, -1);
        var result = new List<StateDiffEnvelope>(values.Length);

        foreach (var value in values)
        {
            if (!value.HasValue)
            {
                continue;
            }

            var diff = JsonSerializer.Deserialize<StateDiffEnvelope>(value.ToString(), JsonOptions());
            if (diff is null)
            {
                continue;
            }

            if (diff.SessionVersion > lastKnownVersion)
            {
                result.Add(diff);
            }
        }

        return result.OrderBy(x => x.DiffSequence).ToList();
    }

    private static string SnapshotKey(Guid sessionId) => $"session:{sessionId}:snapshot";
    private static string DiffsKey(Guid sessionId) => $"session:{sessionId}:diffs";
    private static string DiffSeqKey(Guid sessionId) => $"session:{sessionId}:diff-seq";
    private static string VersionKey(Guid sessionId) => $"session:{sessionId}:version";
    private static JsonSerializerOptions JsonOptions() => new(JsonSerializerDefaults.Web);
}
