using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Services;

namespace SEBT.Portal.StatePlugins.CO.Cbms.Cache;

internal sealed class CbmsHouseholdCache : ICbmsHouseholdCache
{
    private const string KeyPrefix = "co:cbms:";

    // Sentinel stored in the cache for empty/null CBMS responses so that
    // HybridCache treats the negative result as a hit and does not re-invoke
    // the factory until the NegativeCacheExpiration window elapses.
    // ReferenceEquals is used to detect this sentinel — do not use value equality.
    private static readonly GetAccountDetailsResponse NegativeMarkerResponse = new()
    {
        StdntEnrollDtls = new()
    };

    private readonly HybridCache _hybridCache;
    private readonly IHMACSHA256Hasher _hasher;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<CbmsHouseholdCache> _logger;
    private readonly CbmsHouseholdCacheOptions _options;
    private readonly CbmsFetchAccountDetailsDelegate _fetchFromCbms;
    private readonly ConcurrentDictionary<string, Task> _inFlightRefreshes = new();

    public CbmsHouseholdCache(
        HybridCache hybridCache,
        IHMACSHA256Hasher hasher,
        IHostApplicationLifetime lifetime,
        ILoggerFactory loggerFactory,
        IOptions<CbmsHouseholdCacheOptions> options,
        CbmsFetchAccountDetailsDelegate fetchFromCbms)
    {
        _hybridCache = hybridCache;
        _hasher = hasher;
        _lifetime = lifetime;
        _logger = loggerFactory.CreateLogger<CbmsHouseholdCache>();
        _options = options.Value;
        _fetchFromCbms = fetchFromCbms;
    }

    private string KeyFor(string normalizedPhone) => KeyPrefix + _hasher.Hash(normalizedPhone);

    public async Task<GetAccountDetailsResponse?> GetAsync(string normalizedPhone, CancellationToken cancellationToken)
    {
        var key = KeyFor(normalizedPhone);
        var hybridOptions = new HybridCacheEntryOptions { Expiration = _options.HardExpiration };

        var envelope = await _hybridCache.GetOrCreateAsync(
            key,
            (Phone: normalizedPhone, Cache: this),
            (state, ct) => state.Cache.FetchAndWrapAsync(state.Phone, ct),
            hybridOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (envelope is null) return null;

        // Negative cache: sentinel envelope was stored for an empty/null CBMS response.
        if (ReferenceEquals(envelope.Response, NegativeMarkerResponse)) return null;

        if (DateTimeOffset.UtcNow < envelope.SoftExpiryUtc) return envelope.Response;

        // Stale: return value immediately and trigger background refresh.
        TriggerBackgroundRefresh(key, normalizedPhone);
        return envelope.Response;
    }

    private async ValueTask<CbmsHouseholdCacheEnvelope?> FetchAndWrapAsync(
        string normalizedPhone, CancellationToken cancellationToken)
    {
        var response = await _fetchFromCbms(normalizedPhone, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;

        if (response is null || response.StdntEnrollDtls is null || response.StdntEnrollDtls.Count == 0)
        {
            // Negative cache: store a sentinel envelope with a shorter TTL so the
            // HybridCache layer treats it as a hit and does not re-invoke the factory
            // until NegativeCacheExpiration elapses.
            return new CbmsHouseholdCacheEnvelope(
                Response: NegativeMarkerResponse,
                SoftExpiryUtc: now + _options.NegativeCacheExpiration,
                HardExpiryUtc: now + _options.NegativeCacheExpiration,
                CachedAtUtc: now);
        }

        return new CbmsHouseholdCacheEnvelope(
            Response: response,
            SoftExpiryUtc: now + _options.SoftExpiration,
            HardExpiryUtc: now + _options.HardExpiration,
            CachedAtUtc: now);
    }

    private void TriggerBackgroundRefresh(string key, string normalizedPhone)
    {
        _ = _inFlightRefreshes.GetOrAdd(key, k => RunRefreshAsync(k, normalizedPhone));
    }

    private async Task RunRefreshAsync(string key, string normalizedPhone)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.ApplicationStopping);
            cts.CancelAfter(_options.BackgroundRefreshTimeout);
            var fresh = await FetchAndWrapAsync(normalizedPhone, cts.Token).ConfigureAwait(false);
            if (fresh is not null)
            {
                await _hybridCache.SetAsync(
                    key, fresh,
                    new HybridCacheEntryOptions { Expiration = _options.HardExpiration },
                    cancellationToken: cts.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Background CBMS refresh failed; stale envelope retained");
        }
        finally
        {
            _inFlightRefreshes.TryRemove(key, out _);
        }
    }

    public async Task SetAsync(string normalizedPhone, GetAccountDetailsResponse value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        var key = KeyFor(normalizedPhone);
        var now = DateTimeOffset.UtcNow;
        var envelope = new CbmsHouseholdCacheEnvelope(
            Response: value,
            SoftExpiryUtc: now + _options.SoftExpiration,
            HardExpiryUtc: now + _options.HardExpiration,
            CachedAtUtc: now);

        try
        {
            await _hybridCache.SetAsync(
                key, envelope,
                new HybridCacheEntryOptions { Expiration = _options.HardExpiration },
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache write-through failed for {Prefix}; invalidating to force re-fetch", KeyPrefix + "*");
            try
            {
                await InvalidateAsync(normalizedPhone, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception inner)
            {
                // Best-effort secondary; if Invalidate also fails, log and move on.
                _logger.LogWarning(inner, "Cache invalidation also failed during tripwire");
            }
        }
    }

    public Task InvalidateAsync(string normalizedPhone, CancellationToken cancellationToken)
    {
        var key = KeyFor(normalizedPhone);
        return _hybridCache.RemoveAsync(key, cancellationToken).AsTask();
    }
}
