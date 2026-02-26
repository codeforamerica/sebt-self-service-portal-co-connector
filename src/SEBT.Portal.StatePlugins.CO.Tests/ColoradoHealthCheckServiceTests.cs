using SEBT.Portal.StatesPlugins.Interfaces.Data;

namespace SEBT.Portal.StatePlugins.CO.Tests;

public class ColoradoHealthCheckServiceTests
{
    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenCredentialsNotConfigured()
    {
        // Arrange
        var service = new ColoradoHealthCheckService();

        // Act
        var result = await service.CheckHealthAsync();

        // Assert
        Assert.False(result.IsHealthy);
        var unhealthy = Assert.IsType<HealthCheckResult.Unhealthy>(result);
        Assert.Contains("credentials are not configured", unhealthy.ErrorMessage);
    }
}
