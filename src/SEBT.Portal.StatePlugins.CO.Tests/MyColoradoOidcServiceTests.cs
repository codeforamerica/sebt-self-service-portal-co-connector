using SEBT.Portal.StatePlugins.CO.MyColorado;

namespace SEBT.Portal.StatePlugins.CO.Tests;

public class MyColoradoOidcServiceTests
{
    [Fact]
    public void ValidateIdTokenAsync_throws_when_id_token_empty()
    {
        var options = new MyColoradoOidcOptions
        {
            ClientId = "test-client",
            DiscoveryEndpoint = "https://auth.pingone.com/e8e64475-39e1-43de-964b-3bc2e835a2f5/as/.well-known/openid-configuration"
        };
        var service = new MyColoradoOidcService(options, new HttpClient());

        var ex = Assert.Throws<ArgumentException>(() =>
            service.ValidateIdTokenAsync("").GetAwaiter().GetResult());
        Assert.Contains("ID token", ex.Message);

        var ex2 = Assert.Throws<ArgumentException>(() =>
            service.ValidateIdTokenAsync("   ").GetAwaiter().GetResult());
        Assert.Contains("ID token", ex2.Message);
    }
}
