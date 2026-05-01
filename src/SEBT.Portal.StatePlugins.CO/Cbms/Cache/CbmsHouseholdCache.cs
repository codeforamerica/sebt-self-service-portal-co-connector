using System.Collections.Concurrent;
using System.Text.Json;
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

    // Default System.Text.Json options. The Kiota-generated GetAccountDetailsResponse uses
    // PascalCase property names with no [JsonPropertyName] annotations, so default System.Text.Json
    // PascalCase serialization round-trips cleanly. AdditionalData (IDictionary<string, object>)
    // round-trips as JsonElement values which we never read — only the typed properties matter.
    private static readonly JsonSerializerOptions JsonOptions = new();

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
        var hybridOptions = new HybridCacheEntryOptions
        {
            Expiration = _options.HardExpiration,
            LocalCacheExpiration = _options.LocalCacheExpiration,
        };

        var envelope = await _hybridCache.GetOrCreateAsync(
            key,
            (Phone: normalizedPhone, Cache: this),
            (state, ct) => state.Cache.FetchAndWrapAsync(state.Phone, ct),
            hybridOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (envelope is null) return null;

        var response = DeserializeResponse(envelope.ResponseJson);

        // Negative cache: detect by VALUE (empty rows), not by reference. The cached envelope
        // is JSON; the deserialized Response is a fresh allocation on every read. Empty
        // StdntEnrollDtls is the canonical "no household" shape and works identically across
        // L1 (in-process) and L2 (Redis-deserialized cross-pod) reads.
        if (response is null || response.StdntEnrollDtls is null or { Count: 0 })
        {
            // GetOrCreateAsync stored this entry with HardExpiration as the framework TTL
            // (we can't pass per-result options to GetOrCreateAsync). Re-set with
            // NegativeCacheExpiration so HybridCache also evicts at the right time —
            // otherwise the framework retains the negative result for the full hard window
            // and CBMS never gets re-checked when the household appears within that window.
            // Idempotent: subsequent reads see the same envelope and re-set the (same) shorter TTL.
            var negativeOptions = new HybridCacheEntryOptions
            {
                Expiration = _options.NegativeCacheExpiration,
                LocalCacheExpiration = _options.NegativeCacheExpiration,
            };
            await _hybridCache.SetAsync(key, envelope, negativeOptions, cancellationToken: cancellationToken).ConfigureAwait(false);
            return null;
        }

        if (DateTimeOffset.UtcNow < envelope.SoftExpiryUtc) return response;

        // Stale: return value immediately and trigger background refresh.
        TriggerBackgroundRefresh(key, normalizedPhone);
        return response;
    }

    private static string SerializeResponse(GetAccountDetailsResponse response)
        => JsonSerializer.Serialize(response, JsonOptions);

    private static GetAccountDetailsResponse? DeserializeResponse(string json)
        => string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<GetAccountDetailsResponse>(json, JsonOptions);

    private async ValueTask<CbmsHouseholdCacheEnvelope?> FetchAndWrapAsync(
        string normalizedPhone, CancellationToken cancellationToken)
    {
        var response = await _fetchFromCbms(normalizedPhone, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        var isNegative = response?.StdntEnrollDtls is null or { Count: 0 };

        // Always wrap (even a literal-null CBMS response) so the framework caches an envelope
        // rather than null; an empty Response is the canonical negative-cache shape, and
        // GetAsync detects it by value to work correctly across L2 deserialization boundaries.
        var wrapped = response ?? new GetAccountDetailsResponse { StdntEnrollDtls = new() };
        var soft = isNegative ? _options.NegativeCacheExpiration : _options.SoftExpiration;
        var hard = isNegative ? _options.NegativeCacheExpiration : _options.HardExpiration;

        return new CbmsHouseholdCacheEnvelope(
            ResponseJson: SerializeResponse(wrapped),
            SoftExpiryUtc: now + soft,
            HardExpiryUtc: now + hard,
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
                    new HybridCacheEntryOptions
                    {
                        Expiration = _options.HardExpiration,
                        LocalCacheExpiration = _options.LocalCacheExpiration,
                    },
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
            ResponseJson: SerializeResponse(value),
            SoftExpiryUtc: now + _options.SoftExpiration,
            HardExpiryUtc: now + _options.HardExpiration,
            CachedAtUtc: now);

        try
        {
            await _hybridCache.SetAsync(
                key, envelope,
                new HybridCacheEntryOptions
                {
                    Expiration = _options.HardExpiration,
                    LocalCacheExpiration = _options.LocalCacheExpiration,
                },
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
