using System.Net;
using System.Reflection;
using System.Text;
using SEBT.Portal.StatePlugins.CO.CbmsApi;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Mocks;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;

namespace SEBT.Portal.StatePlugins.CO.Tests.CbmsApi;

/// <summary>
/// Tests for <see cref="ClientCredentialsTokenProvider"/>, including ParseExpiresIn
/// handling of string vs number for expires_in.
/// </summary>
public class ClientCredentialsTokenProviderTests
{
    [Fact]
    public async Task GetAuthorizationTokenAsync_parses_expires_in_when_string()
    {
        // Token JSON with expires_in as string (some OAuth implementations return this)
        var tokenWithStringExpiry = """{"access_token":"test-token","token_type":"Bearer","expires_in":"3600"}""";
        var getAccountDetailsJson = await LoadGetAccountDetailsMockAsync();

        var handler = new TokenAndGetAccountDetailsHandler(tokenWithStringExpiry, getAccountDetailsJson);
        var client = CbmsSebtApiClientFactory.Create(
            "test-id",
            "test-secret",
            CbmsDefaults.SandboxApiBaseUrl,
            CbmsDefaults.SandboxTokenEndpointUrl,
            handler);

        var response = await client.Sebt.GetAccountDetails.PostAsync(
            new GetAccountDetailsRequest { PhnNm = "3035551234" });

        Assert.NotNull(response);
        Assert.NotNull(response.StdntEnrollDtls);
        Assert.NotEmpty(response.StdntEnrollDtls);
    }

    private static async Task<string> LoadGetAccountDetailsMockAsync()
    {
        var resourceName = "SEBT.Portal.StatePlugins.CO.CbmsApi.TestData.CbmsMocks.get-account-details.json";
        var apiAssembly = typeof(CbmsSebtApiClient).Assembly;
        await using var stream = apiAssembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Resource not found: {resourceName}");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private sealed class TokenAndGetAccountDetailsHandler : HttpMessageHandler
    {
        private const string TokenPath = "ext-uat-c-cbms-oauth-app/token";
        private const string ApiBase = "ext-uat-c-cbms-cfa-eapi/api";

        private readonly string _tokenResponse;
        private readonly string _getAccountDetailsResponse;

        public TokenAndGetAccountDetailsHandler(string tokenResponse, string getAccountDetailsResponse)
        {
            _tokenResponse = tokenResponse;
            _getAccountDetailsResponse = getAccountDetailsResponse;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? "";

            if (url.Contains(TokenPath, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_tokenResponse, Encoding.UTF8, "application/json")
                });
            }

            if (url.Contains($"{ApiBase}/sebt/get-account-details", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_getAccountDetailsResponse, Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
