using Microsoft.Extensions.Configuration;
using SEBT.Portal.StatesPlugins.Interfaces.Data;

namespace SEBT.Portal.StatePlugins.CO.Tests.CbmsApi;

/// <summary>
/// Integration test that verifies <see cref="ColoradoHealthCheckService"/> against the CBMS sandbox.
/// Skips gracefully when no credentials are configured.
/// </summary>
[Collection("CbmsSandbox")]
public class CbmsHealthCheckSandboxTests(CbmsSandboxFixture fixture)
{
    private const string SkipReason =
        "CBMS sandbox credentials not configured. " +
        "Set Cbms:SandboxClientId and Cbms:SandboxClientSecret via user-secrets or environment variables.";

    [SkippableFact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenSandboxIsReachable()
    {
        Skip.If(!fixture.CredentialsConfigured, SkipReason);

        // Build a configuration that maps sandbox credentials to the keys the service expects
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<CbmsSandboxFixture>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var sandboxConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cbms:ClientId"] = configuration["Cbms:SandboxClientId"],
                ["Cbms:ClientSecret"] = configuration["Cbms:SandboxClientSecret"],
            })
            .Build();

        var service = new ColoradoHealthCheckService(sandboxConfig);

        var result = await service.CheckHealthAsync();

        Assert.True(result.IsHealthy);
        Assert.IsType<HealthCheckResult.Healthy>(result);
    }
}
