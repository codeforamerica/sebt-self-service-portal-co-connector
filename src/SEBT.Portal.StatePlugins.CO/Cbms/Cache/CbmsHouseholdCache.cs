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

        if (DateTimeOffset.UtcNow < envelope.SoftExpiryUtc) return envelope.Response;

        // Stale: return value immediately and trigger background refresh.
        TriggerBackgroundRefresh(key, normalizedPhone);
        return envelope.Response;
    }

    private async ValueTask<CbmsHouseholdCacheEnvelope?> FetchAndWrapAsync(
        string normalizedPhone, CancellationToken cancellationToken)
    {
        var response = await _fetchFromCbms(normalizedPhone, cancellationToken).ConfigureAwait(false);
        if (response is null || response.StdntEnrollDtls is null || response.StdntEnrollDtls.Count == 0)
        {
            // Negative cache: D2 will switch this to return a sentinel envelope with shorter TTL.
            return null;
        }
        var now = DateTimeOffset.UtcNow;
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

    public Task SetAsync(string normalizedPhone, GetAccountDetailsResponse value, CancellationToken cancellationToken)
        => throw new NotImplementedException("Implemented in Task D3");

    public Task InvalidateAsync(string normalizedPhone, CancellationToken cancellationToken)
        => throw new NotImplementedException("Implemented in Task D3");
}
