using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace SEBT.Portal.StatePlugins.CO.CbmsApi;

/// <summary>
/// Factory for creating <see cref="CbmsSebtApiClient"/> instances with correct Kiota wiring.
/// Uses a shared <see cref="SocketsHttpHandler"/> with pooled connection lifetime for DNS rotation.
/// Each call creates lightweight <see cref="HttpClient"/> instances that share the handler's connection pool.
/// </summary>
public static class CbmsSebtApiClientFactory
{
    private static readonly Lazy<SocketsHttpHandler> SharedHandler = new(() => new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    });

    /// <summary>
    /// Creates a client configured for the specified CBMS environment.
    /// Use <see cref="CbmsDefaults"/> constants for well-known environment URLs.
    /// </summary>
    /// <param name="clientId">OAuth 2.0 client ID.</param>
    /// <param name="clientSecret">OAuth 2.0 client secret.</param>
    /// <param name="apiBaseUrl">Base URL for the CBMS API.</param>
    /// <param name="tokenEndpointUrl">OAuth 2.0 token endpoint URL.</param>
    public static CbmsSebtApiClient Create(
        string clientId,
        string clientSecret,
        string apiBaseUrl,
        string tokenEndpointUrl)
    {
        var tokenHttpClient = new HttpClient(SharedHandler.Value, disposeHandler: false);
        var tokenProvider = new ClientCredentialsTokenProvider(
            tokenHttpClient, clientId, clientSecret, tokenEndpointUrl);

        var authProvider = new BaseBearerTokenAuthenticationProvider(tokenProvider);

        var apiHttpClient = new HttpClient(SharedHandler.Value, disposeHandler: false)
        {
            BaseAddress = new Uri(apiBaseUrl),
        };
        var adapter = new HttpClientRequestAdapter(authProvider, httpClient: apiHttpClient);

        return new CbmsSebtApiClient(adapter);
    }
}
