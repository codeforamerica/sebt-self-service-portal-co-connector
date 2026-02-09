namespace SEBT.Portal.StatePlugins.CO.Tests;

public class ColoradoSummerEbtCaseServiceTests
{
    [Fact]
    public async Task GetHouseholdCases()
    {
        // Arrange
        var service = new ColoradoSummerEbtCaseService();
        
        // Act/Assert
        var ex = await Assert.ThrowsAsync<NotImplementedException>(async () => 
            await service.GetHouseholdCases());

        Assert.Contains("Colorado", ex.Message);
    }
}