using Microsoft.Extensions.Configuration;
using SEBT.Portal.StatesPlugins.Interfaces.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.StatePlugins.CO.Tests;

public class ColoradoSummerEbtCaseServiceTests
{
    private static IConfiguration CreateEmptyConfiguration()
    {
        return new ConfigurationBuilder().Build();
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
    public async Task GetHouseholdByGuardianEmailAsync_throws_NotImplementedException()
    {
        var service = new ColoradoSummerEbtCaseService(CreateEmptyConfiguration());
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var ex = await Assert.ThrowsAsync<NotImplementedException>(async () =>
            await service.GetHouseholdByGuardianEmailAsync("test@example.com", piiVisibility, IdentityAssuranceLevel.None));

        Assert.Contains("Colorado", ex.Message);
    }

    [Fact]
    public async Task GetHouseholdCases_throws_NotImplementedException()
    {
        var service = new ColoradoSummerEbtCaseService(CreateEmptyConfiguration());

        var ex = await Assert.ThrowsAsync<NotImplementedException>(async () =>
            await service.GetHouseholdCases());

        Assert.Contains("Colorado", ex.Message);
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