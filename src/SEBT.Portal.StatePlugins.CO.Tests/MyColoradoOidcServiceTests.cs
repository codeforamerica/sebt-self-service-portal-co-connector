using SEBT.Portal.StatePlugins.CO.MyColorado;

namespace SEBT.Portal.StatePlugins.CO.Tests;

public class MyColoradoOidcServiceTests
{
    [Fact]
    public void PrepareAuthorizationAsync_throws_when_redirect_uri_not_allowed()
    {
        var options = new MyColoradoOidcOptions
        {
            ClientId = "test-client",
            ClientSecret = "secret",
            RedirectUris = new List<string> { "http://localhost:8080/callback" }
        };
        var store = new InMemoryPendingLoginStore();
        var service = new MyColoradoOidcService(options, store, new HttpClient());

        var ex = Assert.Throws<ArgumentException>(() =>
            service.PrepareAuthorizationAsync("http://evil.com/callback").GetAwaiter().GetResult());

        Assert.Contains("not allowed", ex.Message);
    }

    [Fact]
    public async Task PrepareAuthorizationAsync_returns_url_and_state_and_stores_pending_login()
    {
        var redirectUri = "http://localhost:8080/callback";
        var options = new MyColoradoOidcOptions
        {
            DiscoveryEndpoint = "https://auth.pingone.com/e8e64475-39e1-43de-964b-3bc2e835a2f5/as/.well-known/openid-configuration",
            Authority = "https://auth.pingone.com/e8e64475-39e1-43de-964b-3bc2e835a2f5/as",
            ClientId = "test-client",
            ClientSecret = "test-secret",
            RedirectUris = new List<string> { redirectUri },
            Scopes = "openid"
        };
        var store = new InMemoryPendingLoginStore();
        var service = new MyColoradoOidcService(options, store, new HttpClient());

        var (authorizationUrl, state) = await service.PrepareAuthorizationAsync(redirectUri);

        Assert.NotNull(state);
        Assert.NotEmpty(state);
        Assert.StartsWith("https://auth.pingone.com/", authorizationUrl);
        Assert.Contains("response_type=code", authorizationUrl);
        Assert.Contains("code_challenge=", authorizationUrl);
        Assert.Contains("code_challenge_method=S256", authorizationUrl);
        Assert.Contains("state=", authorizationUrl);
        Assert.Contains("nonce=", authorizationUrl);
        Assert.Contains("redirect_uri=", authorizationUrl);
        Assert.Contains("client_id=test-client", authorizationUrl);

        var pending = await store.GetAndRemovePendingLoginAsync(state);
        Assert.NotNull(pending);
        Assert.Equal(redirectUri, pending.RedirectUri);
        Assert.NotEmpty(pending.CodeVerifier);
        Assert.NotEmpty(pending.Nonce);

        var pendingGone = await store.GetAndRemovePendingLoginAsync(state);
        Assert.Null(pendingGone);
    }
}
