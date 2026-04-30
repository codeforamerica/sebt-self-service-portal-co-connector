using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Hybrid;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms.Cache;

/// <summary>
/// Minimal HybridCache stand-in for unit tests. Provides per-key coalescing
/// for GetOrCreateAsync, immediate Set/Remove, and a way to inspect cached entries.
/// Does NOT honor expiration timestamps — tests that need to simulate expiration
/// should call RemoveAsync directly to evict.
/// </summary>
internal sealed class InMemoryHybridCache : HybridCache
{
    private readonly ConcurrentDictionary<string, object?> _store = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public int FactoryInvocations { get; private set; }

    public IReadOnlyCollection<string> Keys => _store.Keys.ToArray();

    public bool TryGet<T>(string key, out T? value)
    {
        if (_store.TryGetValue(key, out var raw) && raw is T t)
        {
            value = t;
            return true;
        }
        value = default;
        return false;
    }

    public override async ValueTask<T> GetOrCreateAsync<TState, T>(
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(key, out var existing) && existing is T cached)
        {
            return cached;
        }

        var sem = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_store.TryGetValue(key, out existing) && existing is T cached2)
            {
                return cached2;
            }
            FactoryInvocations++;
            var produced = await factory(state, cancellationToken).ConfigureAwait(false);
            _store[key] = produced;
            return produced;
        }
        finally
        {
            sem.Release();
        }
    }

    public override async ValueTask SetAsync<T>(
        string key,
        T value,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        _store[key] = value;
    }

    public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.TryRemove(key, out _);
        return ValueTask.CompletedTask;
    }

    public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}
