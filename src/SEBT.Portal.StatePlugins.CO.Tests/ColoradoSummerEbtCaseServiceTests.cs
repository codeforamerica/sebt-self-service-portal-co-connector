using SEBT.Portal.StatesPlugins.Interfaces.Models;

namespace SEBT.Portal.StatePlugins.CO.Tests;

public class ColoradoSummerEbtCaseServiceTests
{
    [Fact]
    public async Task GetHouseholdByGuardianEmailAsync_throws_NotImplementedException()
    {
        var service = new ColoradoSummerEbtCaseService();
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var ex = await Assert.ThrowsAsync<NotImplementedException>(async () =>
            await service.GetHouseholdByGuardianEmailAsync("test@example.com", piiVisibility, IdentityAssuranceLevel.None));

        Assert.Contains("Colorado", ex.Message);
    }
}