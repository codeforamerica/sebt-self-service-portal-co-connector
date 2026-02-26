using System.Composition;
using Microsoft.Extensions.Configuration;
using SEBT.Portal.StatePlugins.CO.CbmsApi;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Data;

namespace SEBT.Portal.StatePlugins.CO;

/// <summary>
/// Colorado health check: verifies connectivity to the CBMS SEBT API via its ping endpoint.
/// </summary>
[Export(typeof(IStatePlugin))]
[ExportMetadata("StateCode", "CO")]
[method: ImportingConstructor]
public class ColoradoHealthCheckService([Import(AllowDefault = true)] IConfiguration? configuration = null)
    : IStateHealthCheckService
{
    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var clientId = configuration?["Cbms:ClientId"]
                ?? Environment.GetEnvironmentVariable("Cbms__ClientId");

            var clientSecret = configuration?["Cbms:ClientSecret"]
                ?? Environment.GetEnvironmentVariable("Cbms__ClientSecret");

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                return new HealthCheckResult.Unhealthy(
                    "CBMS credentials are not configured. " +
                    "Set Cbms:ClientId and Cbms:ClientSecret in appsettings or Cbms__ClientId and Cbms__ClientSecret environment variables.");
            }

            var apiBaseUrl = configuration?["Cbms:ApiBaseUrl"]
                ?? Environment.GetEnvironmentVariable("Cbms__ApiBaseUrl")
                ?? CbmsDefaults.SandboxApiBaseUrl;

            var tokenEndpointUrl = configuration?["Cbms:TokenEndpointUrl"]
                ?? Environment.GetEnvironmentVariable("Cbms__TokenEndpointUrl")
                ?? CbmsDefaults.SandboxTokenEndpointUrl;

            var client = CbmsSebtApiClientFactory.Create(clientId, clientSecret, apiBaseUrl, tokenEndpointUrl);
            await client.Ping.GetAsync(cancellationToken: cancellationToken);

            return new HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult.Unhealthy(ex.Message, ex);
        }
    }
}
