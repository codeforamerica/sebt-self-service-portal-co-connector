using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using SEBT.Portal.StatePlugins.CO.Cbms;
using SEBT.Portal.StatePlugins.CO.CbmsApi;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Mocks;

namespace SEBT.Portal.StatePlugins.CO;

public abstract class ColoradoCbmsServiceBase
{
    private readonly object _clientCacheLock = new();
    private readonly HybridCache? _cache;
    private readonly ILogger _logger;
    
    private CbmsConnectionOptions? _cachedOptions;
    private CbmsSebtApiClient? _cachedClient;

    protected ColoradoCbmsServiceBase(HybridCache? cache, ILogger logger)
    {
        _cache = cache;
        _logger = logger;
    }
    
    protected CbmsSebtApiClient GetOrCreateClient(CbmsConnectionOptions options)
    {
        lock (_clientCacheLock)
        {
            if (_cachedOptions == options && _cachedClient != null)
                return _cachedClient;

            HttpMessageHandler? handler = null;
            var clientId = options.ClientId;
            var clientSecret = options.ClientSecret;

            if (options.UseMockResponses)
            {
                if (_cache == null)
                {
                    throw new InvalidOperationException(
                        "HybridCache must be registered in DI when Cbms:UseMockResponses is enabled. " +
                        "Ensure services.AddHybridCache() is called during service registration.");
                }

                clientId = "mock-client-id";
                clientSecret = "mock-client-secret";
                var dataStore = new MockCbmsDataStore(_cache);
                handler = new MockCbmsHttpHandler(dataStore);
            }

            _cachedClient = CbmsSebtApiClientFactory.Create(
                clientId,
                clientSecret,
                options.ApiBaseUrl,
                options.TokenEndpointUrl,
                handler,
                _logger);
            _cachedOptions = options;
            return _cachedClient;
        }
    }
}