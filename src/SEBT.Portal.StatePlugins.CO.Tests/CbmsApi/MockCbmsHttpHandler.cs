using System.Net;
using System.Net.Http;
using System.Text;

namespace SEBT.Portal.StatePlugins.CO.Tests.CbmsApi;

/// <summary>
/// HttpMessageHandler that returns mock CBMS API responses from the OpenAPI spec examples.
/// Used when Cbms:UseMockResponses is enabled so integration tests can run without real sandbox credentials.
/// </summary>
internal sealed class MockCbmsHttpHandler : HttpMessageHandler
{
    private const string TokenPath = "ext-uat-c-cbms-oauth-app/token";
    private const string ApiBase = "int-uat-c-cbms-cfa-eapi/api";

    private static readonly string MockTokenResponse = """{"access_token":"mock-token-for-testing","token_type":"Bearer","expires_in":6000}""";

    private static readonly string MockPingResponse = """{"response":"success"}""";

    private static readonly string MockCheckEnrollmentResponse = """
        {
          "respCd": "200",
          "respMsg": "Success",
          "stdntDtls": [
            {
              "stdFstNm": "John",
              "stdLstNm": "Doe",
              "stdDob": "2015-05-15",
              "mtchCnfd": 95,
              "stdntEligSts": "ELIGIBLE",
              "sebtYear": "2024",
              "sebtAppId": "APP12345",
              "sebtChldId": "CHLD9876",
              "stdReqInd": "REQ-001"
            }
          ]
        }
        """;

    private static readonly string MockGetAccountDetailsResponse = """
        {
          "stdntEnrollDtls": [
            {
              "gurdFstNm": "Jane",
              "gurdLstNm": "Smith",
              "gurdPhnNm": "555-0123",
              "gurdEmailAddr": "jane.smith@example.com",
              "sebtYear": "2024",
              "sebtAppId": "APP-556677",
              "stdFstNm": "Johnny",
              "stdLstNm": "Smith",
              "stdDob": "2016-10-20",
              "mtchCnfd": 100,
              "stdntEligSts": "ENROLLED",
              "sebtAppSts": "APPROVED",
              "eligSrc": "DIRECT_CERT",
              "sebtChldId": "C-990011",
              "sebtChldCwin": "W123456789",
              "addrLn1": "123 Maple Avenue",
              "addrLn2": "Apt 4B",
              "cty": "Denver",
              "staCd": "CO",
              "zip": "80202",
              "zip4": "1234",
              "ebtCardLastFour": "4422",
              "benAvalDt": "2024-06-01",
              "benExpDt": "2024-08-31",
              "ebtCardSts": "ACTIVE",
              "cardIssDt": "2024-05-20",
              "cardBal": 120.50,
              "cbmsCsId": "C8887727",
              "dircEligSrc": "SNAP"
            }
          ]
        }
        """;

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
}
