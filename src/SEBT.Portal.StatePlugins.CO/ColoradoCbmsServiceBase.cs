using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SEBT.Portal.StatePlugins.CO.Cbms;
using SEBT.Portal.StatePlugins.CO.Cbms.Cache;
using SEBT.Portal.StatePlugins.CO.CbmsApi;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Mocks;

namespace SEBT.Portal.StatePlugins.CO;

public abstract class ColoradoCbmsServiceBase
{
    private readonly object _clientCacheLock = new();
    private readonly HybridCache? _cache;
    private readonly ILogger _logger;

    private CbmsConnectionOptions? _cachedOptions;
    private CbmsSebtApiClient? _cachedClient;

    /// <summary>
    /// Plugin-wide household-cache singleton. Read paths use this; write paths
    /// also read from it before PATCHing, and (for address updates) write through.
    /// </summary>
    private protected ICbmsHouseholdCache? HouseholdCache { get; }

    /// <summary>
    /// Full constructor for services that use the household cache (e.g., read paths).
    /// Wires up <see cref="HouseholdCache"/> via <see cref="PluginCache.GetOrBuild"/>.
    /// </summary>
    protected ColoradoCbmsServiceBase(
        IServiceProvider hostProvider,
        IConfiguration configuration,
        HybridCache? cache,
        ILogger logger)
    {
        _cache = cache;
        _logger = logger;
        HouseholdCache = PluginCache.GetOrBuild(hostProvider, configuration);
    }

    /// <summary>
    /// Minimal constructor for services that only need <see cref="GetOrCreateClient"/>
    /// and do not use the household cache. <see cref="HouseholdCache"/> will be null
    /// when constructed this way; call sites must not access it.
    /// </summary>
    protected ColoradoCbmsServiceBase(HybridCache? cache, ILogger logger)
    {
        _cache = cache;
        _logger = logger;
    }

    protected static string FormatPatchBodyForLog(UpdateStudentDetailsRequest b)
    {
        static string Len(string? s) => s is null ? "null" : $"{s.Length}c";
        var a = b.Addr;
        var addr = a is null
            ? "null"
            : $"present(addrLn1={Len(a.AddrLn1)}, addrLn2={Len(a.AddrLn2)}, cty={Len(a.Cty)}, staCd={a.StaCd ?? "null"}, zip={Len(a.Zip)}, zip4={a.Zip4 is not null})";
        return $"sebtChldId={Len(b.SebtChldId)}, sebtAppId={Len(b.SebtAppId)}, addr={addr}, " +
               $"reqNewCard={b.ReqNewCard ?? "null"}, gurdEmailAddr={Len(b.GurdEmailAddr)}, gurdFstNm={Len(b.GurdFstNm)}, gurdLstNm={Len(b.GurdLstNm)}, " +
               $"ntfnOptInSw={b.NtfnOptInSw ?? "null"}, ntfnSrc={b.NtfnSrc ?? "null"}, optOut={b.OptOut ?? "null"}";
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
                clientId, clientSecret,
                options.ApiBaseUrl, options.TokenEndpointUrl,
                handler, _logger);
            _cachedOptions = options;
            return _cachedClient;
        }
    }
}
