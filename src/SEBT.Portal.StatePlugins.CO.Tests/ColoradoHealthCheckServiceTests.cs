using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SEBT.Portal.StatePlugins.CO.Tests;

public class ColoradoHealthCheckServiceTests
{
    [Fact]
    public async Task AlwaysUnhealthyHealthCheck_ReturnsUnhealthy_WithGivenMessage()
    {
        // Arrange
        var message = "CBMS credentials are not configured.";
        var check = new AlwaysUnhealthyHealthCheck(message);

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(message, result.Description);
    }
}
