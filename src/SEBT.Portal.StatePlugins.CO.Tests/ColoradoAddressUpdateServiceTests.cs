using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.StatePlugins.CO.Tests;

public class ColoradoAddressUpdateServiceTests
{
    [Theory]
    [InlineData("3035550199", "3035550199")]
    [InlineData("(303) 555-0199", "3035550199")]
    [InlineData("+1 303 555 0199", "3035550199")]
    public void TryNormalizePhoneNumber_accepts_common_formats(string input, string expected)
    {
        Assert.True(ColoradoAddressUpdateService.TryNormalizePhoneNumber(input, out var phone));
        Assert.Equal(expected, phone);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("user@example.com")]
    public void TryNormalizePhoneNumber_rejects_non_ten_digit_phone(string input)
    {
        Assert.False(ColoradoAddressUpdateService.TryNormalizePhoneNumber(input, out _));
    }

    [Fact]
    public void TryNormalizePhoneNumber_rejects_null()
    {
        Assert.False(ColoradoAddressUpdateService.TryNormalizePhoneNumber(null, out var phone));
        Assert.Equal(string.Empty, phone);
    }

    [Fact]
    public async Task UpdateAddressAsync_returns_success_when_cbms_succeeds()
    {
        var handler = new AddressUpdatePipelineMessageHandler(
            accountDetailsJson: """{"stdntEnrollDtls":[{"sebtChldId":"C-1","sebtAppId":"APP-1","gurdFstNm":"G","gurdLstNm":"H","gurdEmailAddr":"g@example.com"}]}""");

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
    public async Task UpdateAddressAsync_returns_policy_when_multiple_children()
    {
        var handler = new AddressUpdatePipelineMessageHandler(
            accountDetailsJson: """
                {"stdntEnrollDtls":[
                  {"sebtChldId":"C-1","sebtAppId":"A"},
                  {"sebtChldId":"C-2","sebtAppId":"B"}
                ]}
                """);

        var service = new ColoradoAddressUpdateService(CbmsTestConfiguration(), handler);

        var result = await service.UpdateAddressAsync(new AddressUpdateRequest
        {
            HouseholdIdentifierValue = "3035550199",
            Address = ValidAddress()
        });

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("AMBIGUOUS_HOUSEHOLD", result.ErrorCode);
        Assert.False(handler.ReceivedPatch);
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
        private readonly string _accountDetailsJson;

        public bool ReceivedPatch { get; private set; }

        public AddressUpdatePipelineMessageHandler(string? accountDetailsJson = null)
        {
            _accountDetailsJson = accountDetailsJson
                ?? """{"stdntEnrollDtls":[{"sebtChldId":"C-1","sebtAppId":"APP-1"}]}""";
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
                return Task.FromResult(Json(HttpStatusCode.OK, _accountDetailsJson));

            if (url.Contains("update-std-dtls", StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Patch)
            {
                ReceivedPatch = true;
                return Task.FromResult(Json(HttpStatusCode.OK, """{"respCd":"200","respMsg":"Success"}"""));
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
