using System.Collections.Concurrent;

namespace SEBT.Portal.StatePlugins.CO.MyColorado;

/// <summary>
/// In-memory implementation of <see cref="IMyColoradoPendingLoginStore"/>.
/// Suitable for single-instance or testing. For production with multiple instances, use a distributed store.
/// </summary>
public sealed class InMemoryPendingLoginStore : IMyColoradoPendingLoginStore
{
    private readonly ConcurrentDictionary<string, (PendingLoginData Data, DateTimeOffset ExpiresAt)> _store = new();

    public Task SavePendingLoginAsync(string state, PendingLoginData data, TimeSpan expiration, CancellationToken cancellationToken = default)
    {
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
}
