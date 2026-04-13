using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.StatePlugins.CO.Tests;

public class ColoradoAddressUpdateServiceTests
{
    [Theory]
    [InlineData("200", true)]
    [InlineData("00", true)]
    [InlineData("422", false)]
    [InlineData(null, false)]
    public void IsCbmsUpdateSuccessCode_spec_and_UAT(string? respCd, bool expected)
    {
        Assert.Equal(expected, ColoradoAddressUpdateService.IsCbmsUpdateSuccessCode(respCd));
    }

    [Theory]
    [InlineData("""{"respCd":"200","respMsg":"Success"}""")]
    [InlineData("""{"respCd":"00","respMsg":"Success"}""")]
    public async Task UpdateAddressAsync_returns_success_when_cbms_succeeds(string patchResponseJson)
    {
        var handler = new AddressUpdatePipelineMessageHandler(
            accountDetailsJson: """{"stdntEnrollDtls":[{"sebtChldId":1,"sebtAppId":2,"gurdFstNm":"G","gurdLstNm":"H","gurdEmailAddr":"g@example.com"}]}""",
            patchResponseJson: patchResponseJson);

        var service = new ColoradoAddressUpdateService(CbmsTestConfiguration(), handler);
        var request = new AddressUpdateRequest
        {
            HouseholdIdentifierValue = "3035550199",
            Address = ValidAddress()
        };

        var result = await service.UpdateAddressAsync(request);

        Assert.True(result.IsSuccess);
        Assert.True(handler.ReceivedPatch);
    }

    [Theory]
    [InlineData("3035550199")]
    [InlineData("(303) 555-0199")]
    [InlineData("+1 303 555 0199")]
    public async Task UpdateAddressAsync_accepts_formatted_US_phones_like_case_lookup(string householdPhone)
    {
        var handler = new AddressUpdatePipelineMessageHandler();
        var service = new ColoradoAddressUpdateService(CbmsTestConfiguration(), handler);

        var result = await service.UpdateAddressAsync(new AddressUpdateRequest
        {
            HouseholdIdentifierValue = householdPhone,
            Address = ValidAddress()
        });

        Assert.True(result.IsSuccess);
        Assert.True(handler.ReceivedPatch);
    }

    [Fact]
    public async Task UpdateAddressAsync_returns_policy_when_identifier_not_phone()
    {
        var service = new ColoradoAddressUpdateService(
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            new AddressUpdatePipelineMessageHandler());

        var result = await service.UpdateAddressAsync(new AddressUpdateRequest
        {
            HouseholdIdentifierValue = "not-a-phone",
            Address = ValidAddress()
        });

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("INVALID_IDENTIFIER", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAddressAsync_returns_policy_when_identifier_null()
    {
        var service = new ColoradoAddressUpdateService(
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            new AddressUpdatePipelineMessageHandler());

        var result = await service.UpdateAddressAsync(new AddressUpdateRequest
        {
            HouseholdIdentifierValue = null!,
            Address = ValidAddress()
        });

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("INVALID_IDENTIFIER", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAddressAsync_returns_policy_when_address_null()
    {
        var service = new ColoradoAddressUpdateService(CbmsTestConfiguration(), new AddressUpdatePipelineMessageHandler());

        var result = await service.UpdateAddressAsync(new AddressUpdateRequest
        {
            HouseholdIdentifierValue = "3035550199",
            Address = null!
        });

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("INVALID_ADDRESS", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAddressAsync_returns_success_when_row_has_only_sebtAppId()
    {
        var handler = new AddressUpdatePipelineMessageHandler(
            accountDetailsJson: """{"stdntEnrollDtls":[{"sebtAppId":999}]}""");

        var service = new ColoradoAddressUpdateService(CbmsTestConfiguration(), handler);

        var result = await service.UpdateAddressAsync(new AddressUpdateRequest
        {
            HouseholdIdentifierValue = "3035550199",
            Address = ValidAddress()
        });

        Assert.True(result.IsSuccess);
        Assert.True(handler.ReceivedPatch);
    }

    [Fact]
    public async Task UpdateAddressAsync_returns_success_when_multiple_children_sends_array_body()
    {
        var handler = new AddressUpdatePipelineMessageHandler(
            accountDetailsJson: """
                {"stdntEnrollDtls":[
                  {"sebtChldId":1,"sebtAppId":10},
                  {"sebtChldId":2,"sebtAppId":20}
                ]}
                """);

        var service = new ColoradoAddressUpdateService(CbmsTestConfiguration(), handler);

        var result = await service.UpdateAddressAsync(new AddressUpdateRequest
        {
            HouseholdIdentifierValue = "3035550199",
            Address = ValidAddress()
        });

        Assert.True(result.IsSuccess);
        Assert.True(handler.ReceivedPatch);
    }

    [Fact]
    public async Task UpdateAddressAsync_returns_backend_error_when_cbms_not_configured()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cbms:ClientId"] = "",
                ["Cbms:ClientSecret"] = ""
            })
            .Build();

        var service = new ColoradoAddressUpdateService(config, new AddressUpdatePipelineMessageHandler());

        var result = await service.UpdateAddressAsync(new AddressUpdateRequest
        {
            HouseholdIdentifierValue = "3035550199",
            Address = ValidAddress()
        });

        Assert.False(result.IsSuccess);
        Assert.False(result.IsPolicyRejection);
        Assert.Equal("NOT_CONFIGURED", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAddressAsync_returns_backend_error_when_cbms_urls_not_configured()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cbms:ClientId"] = "id",
                ["Cbms:ClientSecret"] = "secret",
                ["Cbms:ApiBaseUrl"] = "",
                ["Cbms:TokenEndpointUrl"] = ""
            })
            .Build();

        var service = new ColoradoAddressUpdateService(config, new AddressUpdatePipelineMessageHandler());

        var result = await service.UpdateAddressAsync(new AddressUpdateRequest
        {
            HouseholdIdentifierValue = "3035550199",
            Address = ValidAddress()
        });

        Assert.False(result.IsSuccess);
        Assert.False(result.IsPolicyRejection);
        Assert.Equal("NOT_CONFIGURED", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAddressAsync_returns_policy_when_no_child_records()
    {
        var handler = new AddressUpdatePipelineMessageHandler(
            accountDetailsJson: """{"stdntEnrollDtls":[]}""");

        var service = new ColoradoAddressUpdateService(CbmsTestConfiguration(), handler);

        var result = await service.UpdateAddressAsync(new AddressUpdateRequest
        {
            HouseholdIdentifierValue = "3035550199",
            Address = ValidAddress()
        });

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("HOUSEHOLD_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAddressAsync_maps_cbms_400_ErrorResponse_to_http_prefixed_message()
    {
        // Shape matches CBMS error JSON (see ErrorResponseSerializationTests); ensures status + detail + correlationId.
        var handler = new AddressUpdatePipelineMessageHandler(
            accountDetailsStatusCode: HttpStatusCode.BadRequest,
            accountDetailsJson: """
                {
                  "apiName": "cbms-sebt-eapi-impl",
                  "correlationId": "11174770-a6a1-4949-b216-622e363e872e",
                  "timestamp": "2026-01-30T16:00:35.143Z",
                  "errorDetails": [ { "code": "400", "message": "Bad Request" } ]
                }
                """);

        var service = new ColoradoAddressUpdateService(CbmsTestConfiguration(), handler);

        var result = await service.UpdateAddressAsync(new AddressUpdateRequest
        {
            HouseholdIdentifierValue = "3035550199",
            Address = ValidAddress()
        });

        Assert.False(result.IsSuccess);
        Assert.False(result.IsPolicyRejection);
        Assert.Equal("CBMS_400", result.ErrorCode);
        Assert.StartsWith("HTTP 400:", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("Bad Request", result.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("correlationId:", result.ErrorMessage, StringComparison.Ordinal);
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

    private static Address ValidAddress() =>
        new()
        {
            StreetAddress1 = "100 Main St",
            City = "Denver",
            State = "CO",
            PostalCode = "80202"
        };

    /// <summary>
    /// Returns OAuth token JSON, a configurable get-account-details body, and success on PATCH update-std-dtls.
    /// </summary>
    private sealed class AddressUpdatePipelineMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _accountDetailsStatusCode;
        private readonly string _accountDetailsJson;
        private readonly string _patchResponseJson;

        public bool ReceivedPatch { get; private set; }

        public AddressUpdatePipelineMessageHandler(
            string? accountDetailsJson = null,
            string? patchResponseJson = null,
            HttpStatusCode accountDetailsStatusCode = HttpStatusCode.OK)
        {
            _accountDetailsStatusCode = accountDetailsStatusCode;
            _accountDetailsJson = accountDetailsJson
                ?? """{"stdntEnrollDtls":[{"sebtChldId":1,"sebtAppId":2}]}""";
            _patchResponseJson = patchResponseJson
                ?? """{"respCd":"200","respMsg":"Success"}""";
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? "";
            if (url.Contains("token", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
            {
                const string tokenJson = """{"access_token":"test-token","token_type":"Bearer","expires_in":3600}""";
                return Task.FromResult(Json(HttpStatusCode.OK, tokenJson));
            }

            if (url.Contains("get-account-details", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Post)
                return Task.FromResult(Json(_accountDetailsStatusCode, _accountDetailsJson));

            if (url.Contains("update-std-dtls", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Patch)
            {
                ReceivedPatch = true;
                return Task.FromResult(Json(HttpStatusCode.OK, _patchResponseJson));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
            new(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
    }
}
