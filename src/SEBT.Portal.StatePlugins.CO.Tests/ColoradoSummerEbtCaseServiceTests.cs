using SEBT.Portal.StatesPlugins.Interfaces.Models;

namespace SEBT.Portal.StatePlugins.CO.Tests;

public class ColoradoSummerEbtCaseServiceTests
{
    [Fact]
    public async Task GetHouseholdByGuardianEmailAsync_ThrowsNotImplementedException()
    {
        // Arrange
        var service = new ColoradoSummerEbtCaseService();
        var piiVisibility = new PiiVisibility(false, false, false);

        // Act/Assert
        var ex = await Assert.ThrowsAsync<NotImplementedException>(async () =>
            await service.GetHouseholdByGuardianEmailAsync(
                "test@example.com",
                piiVisibility,
                IdentityAssuranceLevel.IAL1));

        Assert.Contains("Colorado", ex.Message);
    }

    [Fact]
    public async Task GetHouseholdByGuardianPhoneAsync_ThrowsInvalidOperationException_WhenConfigurationMissing()
    {
        // Arrange — no configuration provided, so CBMS credentials are absent
        var service = new ColoradoSummerEbtCaseService();
        var piiVisibility = new PiiVisibility(false, false, false);

        // Act/Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.GetHouseholdByGuardianPhoneAsync(
                "8005551234",
                piiVisibility,
                IdentityAssuranceLevel.IAL1));
    }
}
