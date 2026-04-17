using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.StatePlugins.CO.Tests;

public class ColoradoCardReplacementServiceTests
{
    [Theory]
    [InlineData("""{"respCd":"200","respMsg":"Success"}""")]
    [InlineData("""{"respCd":"00","respMsg":"Success"}""")]
    public async Task RequestCardReplacementAsync_returns_success_when_cbms_succeeds(string patchResponseJson)
    {
        var handler = new CardReplacementPipelineMessageHandler(
            accountDetailsJson: SingleCwinEnrollmentJson(cwin: "8912650", chldId: 1, appId: 2),
            patchResponseJson: patchResponseJson);

        var service = new ColoradoCardReplacementService(CbmsTestConfiguration(), handler);

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "3035550199",
            CaseIds = ["8912650"],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.True(result.IsSuccess);
        Assert.True(handler.ReceivedPatch);
    }

    [Theory]
    [InlineData("3035550199")]
    [InlineData("(303) 555-0199")]
    [InlineData("+1 303 555 0199")]
    public async Task RequestCardReplacementAsync_accepts_formatted_US_phones_like_case_lookup(string householdPhone)
    {
        var handler = new CardReplacementPipelineMessageHandler(
            accountDetailsJson: SingleCwinEnrollmentJson(cwin: "8912650", chldId: 1, appId: 2));

        var service = new ColoradoCardReplacementService(CbmsTestConfiguration(), handler);

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = householdPhone,
            CaseIds = ["8912650"],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.True(result.IsSuccess);
        Assert.True(handler.ReceivedPatch);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_returns_policy_when_identifier_not_phone()
    {
        var service = new ColoradoCardReplacementService(
            CbmsTestConfiguration(),
            new CardReplacementPipelineMessageHandler());

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "not-a-phone",
            CaseIds = ["8912650"],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("INVALID_IDENTIFIER", result.ErrorCode);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_returns_policy_when_identifier_null()
    {
        var service = new ColoradoCardReplacementService(
            CbmsTestConfiguration(),
            new CardReplacementPipelineMessageHandler());

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = null!,
            CaseIds = ["8912650"],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("INVALID_IDENTIFIER", result.ErrorCode);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_returns_policy_when_case_ids_empty()
    {
        var service = new ColoradoCardReplacementService(
            CbmsTestConfiguration(),
            new CardReplacementPipelineMessageHandler());

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "3035550199",
            CaseIds = [],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("INVALID_CASE_IDS", result.ErrorCode);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_returns_backend_error_when_cbms_not_configured()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cbms:ClientId"] = "",
                ["Cbms:ClientSecret"] = ""
            })
            .Build();

        var service = new ColoradoCardReplacementService(config, new CardReplacementPipelineMessageHandler());

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "3035550199",
            CaseIds = ["8912650"],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.False(result.IsSuccess);
        Assert.False(result.IsPolicyRejection);
        Assert.Equal("NOT_CONFIGURED", result.ErrorCode);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_returns_policy_when_no_enrollment_rows()
    {
        var handler = new CardReplacementPipelineMessageHandler(
            accountDetailsJson: """{"stdntEnrollDtls":[]}""");

        var service = new ColoradoCardReplacementService(CbmsTestConfiguration(), handler);

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "3035550199",
            CaseIds = ["8912650"],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("HOUSEHOLD_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_returns_policy_when_case_ids_do_not_match_any_row()
    {
        var handler = new CardReplacementPipelineMessageHandler(
            accountDetailsJson: SingleCwinEnrollmentJson(cwin: "8912650", chldId: 1, appId: 2));

        var service = new ColoradoCardReplacementService(CbmsTestConfiguration(), handler);

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "3035550199",
            CaseIds = ["9999999"],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("CASES_NOT_FOUND", result.ErrorCode);
        Assert.False(handler.ReceivedPatch);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_fails_when_some_case_ids_do_not_match()
    {
        var handler = new CardReplacementPipelineMessageHandler(
            accountDetailsJson: SingleCwinEnrollmentJson(cwin: "8912650", chldId: 1, appId: 2));

        var service = new ColoradoCardReplacementService(CbmsTestConfiguration(), handler);

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "3035550199",
            CaseIds = ["8912650", "9999999"],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("CASES_NOT_FOUND", result.ErrorCode);
        Assert.False(handler.ReceivedPatch);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_sends_multi_body_array_when_multiple_cases_match()
    {
        var handler = new CardReplacementPipelineMessageHandler(
            accountDetailsJson: """
                {"stdntEnrollDtls":[
                  {"sebtChldCwin":1001,"sebtChldId":1,"sebtAppId":10},
                  {"sebtChldCwin":1002,"sebtChldId":2,"sebtAppId":20}
                ]}
                """);

        var service = new ColoradoCardReplacementService(CbmsTestConfiguration(), handler);

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "3035550199",
            CaseIds = ["1001", "1002"],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.True(result.IsSuccess);
        Assert.True(handler.ReceivedPatch);
        Assert.Equal(2, handler.LastPatchBodyElementCount);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_sets_reqNewCard_Y_on_each_body()
    {
        var handler = new CardReplacementPipelineMessageHandler(
            accountDetailsJson: SingleCwinEnrollmentJson(cwin: "8912650", chldId: 1, appId: 2));

        var service = new ColoradoCardReplacementService(CbmsTestConfiguration(), handler);

        await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "3035550199",
            CaseIds = ["8912650"],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.True(handler.ReceivedPatch);
        Assert.Contains("\"reqNewCard\":\"Y\"", handler.LastPatchBodyRaw);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_returns_backend_error_when_cbms_returns_unexpected_respCd()
    {
        var handler = new CardReplacementPipelineMessageHandler(
            accountDetailsJson: SingleCwinEnrollmentJson(cwin: "8912650", chldId: 1, appId: 2),
            patchResponseJson: """{"respCd":"422","respMsg":"Unprocessable"}""");

        var service = new ColoradoCardReplacementService(CbmsTestConfiguration(), handler);

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "3035550199",
            CaseIds = ["8912650"],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.False(result.IsSuccess);
        Assert.False(result.IsPolicyRejection);
        Assert.Equal("CBMS_UPDATE_FAILED", result.ErrorCode);
        Assert.Contains("Unprocessable", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_maps_cbms_400_ErrorResponse_to_http_prefixed_message()
    {
        var handler = new CardReplacementPipelineMessageHandler(
            accountDetailsStatusCode: HttpStatusCode.BadRequest,
            accountDetailsJson: """
                {
                  "apiName": "cbms-sebt-eapi-impl",
                  "correlationId": "11174770-a6a1-4949-b216-622e363e872e",
                  "timestamp": "2026-01-30T16:00:35.143Z",
                  "errorDetails": [ { "code": "400", "message": "Bad Request" } ]
                }
                """);

        var service = new ColoradoCardReplacementService(CbmsTestConfiguration(), handler);

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "3035550199",
            CaseIds = ["8912650"],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.False(result.IsSuccess);
        Assert.False(result.IsPolicyRejection);
        Assert.Equal("CBMS_400", result.ErrorCode);
        Assert.StartsWith("HTTP 400:", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("Bad Request", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("correlationId:", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_maps_cbms_404_to_policy_rejection()
    {
        var handler = new CardReplacementPipelineMessageHandler(
            accountDetailsStatusCode: HttpStatusCode.NotFound,
            accountDetailsJson: """
                {
                  "apiName": "cbms-sebt-eapi-impl",
                  "correlationId": "x",
                  "timestamp": "2026-01-30T16:00:35.143Z",
                  "errorDetails": [ { "code": "404", "message": "Not Found" } ]
                }
                """);

        var service = new ColoradoCardReplacementService(CbmsTestConfiguration(), handler);

        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "3035550199",
            CaseIds = ["8912650"],
            Reason = CardReplacementReason.Unspecified
        });

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("CBMS_404", result.ErrorCode);
    }

    private static IConfiguration CbmsTestConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cbms:ClientId"] = "id",
                ["Cbms:ClientSecret"] = "secret",
                ["Cbms:ApiBaseUrl"] = "https://cbms-api.test/",
                ["Cbms:TokenEndpointUrl"] = "https://cbms-auth.test/token"
            })
            .Build();

    private static string SingleCwinEnrollmentJson(string cwin, int chldId, int appId) =>
        $$"""{"stdntEnrollDtls":[{"sebtChldCwin":{{cwin}},"sebtChldId":{{chldId}},"sebtAppId":{{appId}}}]}""";

    /// <summary>
    /// Returns OAuth token, a configurable get-account-details body, and success on PATCH update-std-dtls.
    /// Captures the PATCH body so tests can assert on its shape.
    /// </summary>
    private sealed class CardReplacementPipelineMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _accountDetailsStatusCode;
        private readonly string _accountDetailsJson;
        private readonly string _patchResponseJson;

        public bool ReceivedPatch { get; private set; }
        public string LastPatchBodyRaw { get; private set; } = string.Empty;
        public int LastPatchBodyElementCount { get; private set; }

        public CardReplacementPipelineMessageHandler(
            string? accountDetailsJson = null,
            string? patchResponseJson = null,
            HttpStatusCode accountDetailsStatusCode = HttpStatusCode.OK)
        {
            _accountDetailsStatusCode = accountDetailsStatusCode;
            _accountDetailsJson = accountDetailsJson
                ?? """{"stdntEnrollDtls":[{"sebtChldCwin":8912650,"sebtChldId":1,"sebtAppId":2}]}""";
            _patchResponseJson = patchResponseJson
                ?? """{"respCd":"200","respMsg":"Success"}""";
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? "";
            if (url.Contains("token", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
            {
                const string tokenJson = """{"access_token":"test-token","token_type":"Bearer","expires_in":3600}""";
                return Json(HttpStatusCode.OK, tokenJson);
            }

            if (url.Contains("get-account-details", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
                return Json(_accountDetailsStatusCode, _accountDetailsJson);

            if (url.Contains("update-std-dtls", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Patch)
            {
                ReceivedPatch = true;
                LastPatchBodyRaw = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
                LastPatchBodyElementCount = CountTopLevelArrayElements(LastPatchBodyRaw);
                return Json(HttpStatusCode.OK, _patchResponseJson);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
        }

        private static int CountTopLevelArrayElements(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return 0;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                return doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array
                    ? doc.RootElement.GetArrayLength()
                    : 1;
            }
            catch
            {
                return 0;
            }
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
            new(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
    }
}
