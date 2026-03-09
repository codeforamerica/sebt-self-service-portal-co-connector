using Microsoft.Extensions.Configuration;
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

    private static IConfiguration CreateCbmsConfiguration(string clientId = "test-id", string clientSecret = "test-secret")
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cbms:ClientId"] = clientId,
                ["Cbms:ClientSecret"] = clientSecret
            })
            .Build();
    }

    [Fact]
    public async Task GetHouseholdByGuardianEmailAsync_returns_null_CBMS_has_no_email_lookup()
    {
        var service = new ColoradoSummerEbtCaseService(CreateEmptyConfiguration());
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = await service.GetHouseholdByGuardianEmailAsync(
            "test@example.com", piiVisibility, IdentityAssuranceLevel.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetHouseholdCases_returns_empty_when_no_household_id()
    {
        var service = new ColoradoSummerEbtCaseService(CreateEmptyConfiguration());

        var result = await service.GetHouseholdCases();

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetHouseholdCasesAsync_with_email_returns_empty_CBMS_has_no_email_lookup()
    {
        var service = new ColoradoSummerEbtCaseService(CreateEmptyConfiguration());

        var result = await service.GetHouseholdCasesAsync("guardian@example.com");

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetHouseholdCasesAsync_with_null_or_empty_returns_empty()
    {
        var service = new ColoradoSummerEbtCaseService(CreateEmptyConfiguration());

        var resultNull = await service.GetHouseholdCasesAsync(null);
        var resultEmpty = await service.GetHouseholdCasesAsync("");

        Assert.NotNull(resultNull);
        Assert.Empty(resultNull);
        Assert.NotNull(resultEmpty);
        Assert.Empty(resultEmpty);
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_returns_null_for_unsupported_identifier_type()
    {
        var service = new ColoradoSummerEbtCaseService(CreateEmptyConfiguration());
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
        var service = new ColoradoSummerEbtCaseService(CreateEmptyConfiguration());
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
        var service = new ColoradoSummerEbtCaseService(CreateCbmsConfiguration());
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = await service.GetHouseholdByIdentifierAsync(
            HouseholdIdentifierType.Phone,
            phone ?? string.Empty,
            piiVisibility,
            IdentityAssuranceLevel.None);

        Assert.Null(result);
    }
}