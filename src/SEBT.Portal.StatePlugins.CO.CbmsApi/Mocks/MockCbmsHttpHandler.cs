using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;

namespace SEBT.Portal.StatePlugins.CO.CbmsApi.Mocks;

/// <summary>
/// HttpMessageHandler that returns mock CBMS API responses from the OpenAPI spec examples.
/// Used when Cbms:UseMockResponses is enabled for integration tests and local development
/// without real sandbox credentials.
/// </summary>
/// <remarks>
/// Mock responses are loaded from embedded JSON files in TestData/CbmsMocks/.
/// Edit those files to change mock data without recompiling core logic.
/// </remarks>
public sealed class MockCbmsHttpHandler : HttpMessageHandler
{
    private const string TokenPath = "ext-uat-c-cbms-oauth-app/token";
    private const string ApiBase = "ext-uat-c-cbms-cfa-eapi/api";

    private static readonly string MockTokenResponse = LoadMockJson("token.json");
    private static readonly string MockPingResponse = LoadMockJson("ping.json");
    private static readonly string MockCheckEnrollmentResponse = LoadMockJson("check-enrollment.json");
    private static readonly string MockGetAccountDetailsResponse = LoadMockJson("get-account-details.json");

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken) =>
        SendAsync(request, cancellationToken).GetAwaiter().GetResult();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri?.ToString() ?? "";
        var method = request.Method;

        if (url.Contains(TokenPath, StringComparison.OrdinalIgnoreCase) && method == HttpMethod.Post)
        {
            return Task.FromResult(JsonResponse(MockTokenResponse));
        }

        if (url.Contains($"{ApiBase}/ping", StringComparison.OrdinalIgnoreCase) && method == HttpMethod.Get)
        {
            return Task.FromResult(JsonResponse(MockPingResponse));
        }

        if (url.Contains($"{ApiBase}/sebt/check-enrollment", StringComparison.OrdinalIgnoreCase) && method == HttpMethod.Post)
        {
            return Task.FromResult(JsonResponse(MockCheckEnrollmentResponse));
        }

        if (url.Contains($"{ApiBase}/sebt/get-account-details", StringComparison.OrdinalIgnoreCase) && method == HttpMethod.Post)
        {
            return Task.FromResult(JsonResponse(MockGetAccountDetailsResponse));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent($$"""{"error":"Mock handler: no response for {{method}} {{url}}"}""", Encoding.UTF8, "application/json")
        });
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private static string LoadMockJson(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"SEBT.Portal.StatePlugins.CO.CbmsApi.TestData.CbmsMocks.{fileName}";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new InvalidOperationException(
                $"Mock JSON resource not found: {resourceName}. Ensure TestData/CbmsMocks/{fileName} is set as EmbeddedResource.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
