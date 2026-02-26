using SEBT.Portal.StatesPlugins.Interfaces.Models;

namespace SEBT.Portal.StatePlugins.CO.Tests;

public class ColoradoSummerEbtCaseServiceTests
{
    [Fact]
    public async Task GetHouseholdByGuardianEmailAsync()
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
}