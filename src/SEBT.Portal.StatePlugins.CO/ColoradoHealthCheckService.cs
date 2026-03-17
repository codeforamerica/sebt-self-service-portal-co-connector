using System.Composition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SEBT.Portal.StatesPlugins.Interfaces;

namespace SEBT.Portal.StatePlugins.CO;

/// <summary>
/// Colorado health check: registers a check that verifies connectivity
/// to the CBMS SEBT API via its ping endpoint.
/// </summary>
[Export(typeof(IStatePlugin))]
[ExportMetadata("StateCode", "CO")]
[method: ImportingConstructor]
public class ColoradoHealthCheckService([Import(AllowDefault = true)] IConfiguration? configuration = null)
    : IStateHealthCheckService
{
    private const string CheckName = "co-cbms-api-ping";

    public void ConfigureHealthChecks(IHealthChecksBuilder builder)
    {
        var clientId = configuration?["Cbms:ClientId"]
            ?? Environment.GetEnvironmentVariable("Cbms__ClientId");

        var clientSecret = configuration?["Cbms:ClientSecret"]
            ?? Environment.GetEnvironmentVariable("Cbms__ClientSecret");

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            builder.AddCheck(
                CheckName,
                new AlwaysDegradedHealthCheck(
                    "CBMS credentials are not configured. " +
                    "Set Cbms:ClientId and Cbms:ClientSecret in appsettings or Cbms__ClientId and Cbms__ClientSecret environment variables."),
                HealthStatus.Degraded,
                ["external-api", "co"]);
            return;
        }

        var apiBaseUrl = configuration?["Cbms:ApiBaseUrl"]
            ?? Environment.GetEnvironmentVariable("Cbms__ApiBaseUrl");

        var tokenEndpointUrl = configuration?["Cbms:TokenEndpointUrl"]
            ?? Environment.GetEnvironmentVariable("Cbms__TokenEndpointUrl");

        if (string.IsNullOrWhiteSpace(apiBaseUrl) || string.IsNullOrWhiteSpace(tokenEndpointUrl))
        {
            builder.AddCheck(
                CheckName,
                new AlwaysDegradedHealthCheck(
                    "CBMS API endpoints are not configured. " +
                    "Set Cbms:ApiBaseUrl and Cbms:TokenEndpointUrl in appsettings or Cbms__ApiBaseUrl and Cbms__TokenEndpointUrl environment variables."),
                HealthStatus.Degraded,
                ["external-api", "co"]);
            return;
        }

        builder.AddCheck(
            CheckName,
            new CbmsApiHealthCheck(clientId, clientSecret, apiBaseUrl, tokenEndpointUrl),
            HealthStatus.Degraded,
            ["external-api", "co"]);
    }
}
