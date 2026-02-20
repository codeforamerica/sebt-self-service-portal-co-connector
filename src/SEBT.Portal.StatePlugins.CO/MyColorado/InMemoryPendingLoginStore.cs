using System.Collections.Concurrent;

namespace SEBT.Portal.StatePlugins.CO.MyColorado;

/// <summary>
/// In-memory implementation of <see cref="IMyColoradoPendingLoginStore"/>.
/// Suitable for single-instance or testing. For production with multiple instances, use a distributed store.
/// Expired entries are removed on each write (TTL cleanup) so the store does not grow unbounded.
/// </summary>
public sealed class InMemoryPendingLoginStore : IMyColoradoPendingLoginStore
{
    private readonly ConcurrentDictionary<string, (PendingLoginData Data, DateTimeOffset ExpiresAt)> _store = new();

    public int Count => _store.Count;

    public Task SavePendingLoginAsync(string state, PendingLoginData data, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
        RemoveExpiredEntries();
        _store[state] = (data, DateTimeOffset.UtcNow.Add(expiration));
        return Task.CompletedTask;
    }

    public Task<PendingLoginData?> GetAndRemovePendingLoginAsync(string state, CancellationToken cancellationToken = default)
    {
        if (!_store.TryRemove(state, out var entry))
            return Task.FromResult<PendingLoginData?>(null);
        if (DateTimeOffset.UtcNow > entry.ExpiresAt)
            return Task.FromResult<PendingLoginData?>(null);
        return Task.FromResult<PendingLoginData?>(entry.Data);
    }

    private void RemoveExpiredEntries()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kv in _store)
        {
            if (now > kv.Value.ExpiresAt)
                _store.TryRemove(kv.Key, out _);
        }
    }
}
