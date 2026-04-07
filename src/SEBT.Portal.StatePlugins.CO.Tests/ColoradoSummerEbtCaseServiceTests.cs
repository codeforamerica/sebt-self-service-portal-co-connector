using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

    private static HybridCache CreateInMemoryHybridCache()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<HybridCache>();
    }

    [Fact]
    public async Task GetHouseholdByGuardianEmailAsync_returns_null_CBMS_has_no_email_lookup()
    {
        var service = new ColoradoSummerEbtCaseService(CreateEmptyConfiguration(), NullLoggerFactory.Instance);
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = await service.GetHouseholdByGuardianEmailAsync(
            "test@example.com", piiVisibility, IdentityAssuranceLevel.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_returns_null_for_unsupported_identifier_type()
    {
        var service = new ColoradoSummerEbtCaseService(CreateEmptyConfiguration(), NullLoggerFactory.Instance);
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
        var service = new ColoradoSummerEbtCaseService(CreateEmptyConfiguration(), NullLoggerFactory.Instance);
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
        var cache = CreateInMemoryHybridCache();
        var service = new ColoradoSummerEbtCaseService(
            CreateCbmsConfiguration(useMockResponses: true), NullLoggerFactory.Instance, cache);
        var piiVisibility = new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true);

        var result = await service.GetHouseholdByIdentifierAsync(
            HouseholdIdentifierType.Phone,
            "7198004382",
            piiVisibility,
            IdentityAssuranceLevel.None);

        Assert.NotNull(result);
        Assert.NotEmpty(result.SummerEbtCases);
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_with_Phone_returns_null_for_unknown_phone_in_mock_mode()
    {
        var cache = CreateInMemoryHybridCache();
        var service = new ColoradoSummerEbtCaseService(
            CreateCbmsConfiguration(useMockResponses: true), NullLoggerFactory.Instance, cache);
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = await service.GetHouseholdByIdentifierAsync(
            HouseholdIdentifierType.Phone,
            "9999999999",
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
        var service = new ColoradoSummerEbtCaseService(CreateCbmsConfiguration(), NullLoggerFactory.Instance);
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = await service.GetHouseholdByIdentifierAsync(
            HouseholdIdentifierType.Phone,
            phone ?? string.Empty,
            piiVisibility,
            IdentityAssuranceLevel.None);

        Assert.Null(result);
    }
}
