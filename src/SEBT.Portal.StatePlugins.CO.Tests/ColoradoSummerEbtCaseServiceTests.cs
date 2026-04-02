using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SEBT.Portal.StatePlugins.CO;
using SEBT.Portal.StatePlugins.CO.CbmsApi;
using SEBT.Portal.StatesPlugins.Interfaces.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.StatePlugins.CO.Tests;

public class ColoradoSummerEbtCaseServiceTests
{
    private static IConfiguration CreateEmptyConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Cbms:UseMockResponses"] = "false" })
            .Build();
    }

    private static IConfiguration CreateCbmsConfiguration(
        string clientId = "test-id",
        string clientSecret = "test-secret",
        bool useMockResponses = false)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cbms:ClientId"] = clientId,
                ["Cbms:ClientSecret"] = clientSecret,
                ["Cbms:UseMockResponses"] = useMockResponses ? "true" : "false"
            })
            .Build();
    }

    [Fact]
    public async Task GetHouseholdByGuardianEmailAsync_returns_null_CBMS_has_no_email_lookup()
    {
        var service = new ColoradoSummerEbtCaseService(CreateEmptyConfiguration(), NullLogger<ColoradoSummerEbtCaseService>.Instance);
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = await service.GetHouseholdByGuardianEmailAsync(
            "test@example.com", piiVisibility, IdentityAssuranceLevel.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_returns_null_for_unsupported_identifier_type()
    {
        var service = new ColoradoSummerEbtCaseService(CreateEmptyConfiguration(), NullLogger<ColoradoSummerEbtCaseService>.Instance);
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = await service.GetHouseholdByIdentifierAsync(
            HouseholdIdentifierType.SnapId,
            "snap-123",
            piiVisibility,
            IdentityAssuranceLevel.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_with_Phone_returns_null_when_Cbms_not_configured()
    {
        var service = new ColoradoSummerEbtCaseService(CreateEmptyConfiguration(), NullLogger<ColoradoSummerEbtCaseService>.Instance);
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = await service.GetHouseholdByIdentifierAsync(
            HouseholdIdentifierType.Phone,
            "8185551234",
            piiVisibility,
            IdentityAssuranceLevel.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_with_Phone_returns_household_when_UseMockResponses_and_valid_phone()
    {
        var service = new ColoradoSummerEbtCaseService(CreateCbmsConfiguration(useMockResponses: true), NullLogger<ColoradoSummerEbtCaseService>.Instance);
        var piiVisibility = new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true);

        var result = await service.GetHouseholdByIdentifierAsync(
            HouseholdIdentifierType.Phone,
            "8185551234",
            piiVisibility,
            IdentityAssuranceLevel.None);

        Assert.NotNull(result);
        Assert.Equal("8185551234", result.Phone);
        Assert.NotEmpty(result.SummerEbtCases);
        var @case = result.SummerEbtCases[0];
        Assert.Equal("Johnny", @case.ChildFirstName);
        Assert.Equal("Smith", @case.ChildLastName);
        Assert.Equal("C8887727", @case.EbtCaseNumber);
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_with_Phone_returns_null_when_CBMS_returns_404()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cbms:ClientId"] = "test-id",
                ["Cbms:ClientSecret"] = "test-secret",
                ["Cbms:UseMockResponses"] = "true",
                ["Cbms:Return404ForGetAccountDetails"] = "true"
            })
            .Build();
        var service = new ColoradoSummerEbtCaseService(config, NullLogger<ColoradoSummerEbtCaseService>.Instance);
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = await service.GetHouseholdByIdentifierAsync(
            HouseholdIdentifierType.Phone,
            "8185551234",
            piiVisibility,
            IdentityAssuranceLevel.None);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123")]
    [InlineData("12345")]
    public async Task GetHouseholdByIdentifierAsync_with_Phone_returns_null_when_invalid_phone(string? phone)
    {
        var service = new ColoradoSummerEbtCaseService(CreateCbmsConfiguration(), NullLogger<ColoradoSummerEbtCaseService>.Instance);
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = await service.GetHouseholdByIdentifierAsync(
            HouseholdIdentifierType.Phone,
            phone ?? string.Empty,
            piiVisibility,
            IdentityAssuranceLevel.None);

        Assert.Null(result);
    }
}