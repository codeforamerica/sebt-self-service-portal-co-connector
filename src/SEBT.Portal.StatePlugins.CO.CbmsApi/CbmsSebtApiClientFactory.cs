using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace SEBT.Portal.StatePlugins.CO.CbmsApi;

/// <summary>
/// Factory for creating <see cref="CbmsSebtApiClient"/> instances with correct Kiota wiring.
/// Uses a shared <see cref="SocketsHttpHandler"/> with pooled connection lifetime for DNS rotation
/// and lazily-created singleton <see cref="HttpClient"/> instances per environment.
/// </summary>
public static class CbmsSebtApiClientFactory
{
    private const string SandboxApiBaseUrl =
        "https://test-ch2-api.state.co.us/int-uat-c-cbms-cfa-eapi/api";

    private const string SandboxTokenEndpointUrl =
        "https://test-ch2-api.state.co.us/ext-uat-c-cbms-oauth-app/token";

    private static readonly Lazy<SocketsHttpHandler> SharedHandler = new(() => new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    });

    private static readonly Lazy<HttpClient> SandboxApiHttpClient = new(() =>
        new HttpClient(SharedHandler.Value, disposeHandler: false)
        {
            BaseAddress = new Uri(SandboxApiBaseUrl),
        });

    private static readonly Lazy<HttpClient> TokenHttpClient = new(() =>
        new HttpClient(SharedHandler.Value, disposeHandler: false));

    /// <summary>
    /// Creates a client configured for the CBMS sandbox (UAT) environment.
    /// </summary>
    /// <param name="clientId">OAuth 2.0 client ID for the CBMS sandbox.</param>
    /// <param name="clientSecret">OAuth 2.0 client secret for the CBMS sandbox.</param>
    public static CbmsSebtApiClient CreateSandbox(string clientId, string clientSecret)
    {
        var tokenProvider = new ClientCredentialsTokenProvider(
            TokenHttpClient.Value, clientId, clientSecret, SandboxTokenEndpointUrl);
        var authProvider = new BaseBearerTokenAuthenticationProvider(tokenProvider);
        var adapter = new HttpClientRequestAdapter(authProvider, httpClient: SandboxApiHttpClient.Value);
        return new CbmsSebtApiClient(adapter);
    }
}
