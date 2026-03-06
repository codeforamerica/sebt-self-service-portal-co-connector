using Microsoft.Extensions.Configuration;
using SEBT.Portal.StatePlugins.CO.CbmsApi;

namespace SEBT.Portal.StatePlugins.CO.Cbms;

/// <summary>
/// Reads CBMS configuration using the same keys as <see cref="ColoradoHealthCheckService"/>:
/// Cbms:ClientId, Cbms:ClientSecret, Cbms:ApiBaseUrl, Cbms:TokenEndpointUrl (and Cbms__* env vars).
/// Uses <see cref="CbmsDefaults"/> when URLs are not set.
/// </summary>
internal static class CbmsOptionsHelper
{
    public static CbmsConnectionOptions GetCbmsOptions(IConfiguration configuration)
    {
        var clientId = configuration["Cbms:ClientId"]
            ?? Environment.GetEnvironmentVariable("Cbms__ClientId")
            ?? string.Empty;
        var clientSecret = configuration["Cbms:ClientSecret"]
            ?? Environment.GetEnvironmentVariable("Cbms__ClientSecret")
            ?? string.Empty;
        var apiBaseUrl = configuration["Cbms:ApiBaseUrl"]
            ?? Environment.GetEnvironmentVariable("Cbms__ApiBaseUrl")
            ?? CbmsDefaults.SandboxApiBaseUrl;
        var tokenEndpointUrl = configuration["Cbms:TokenEndpointUrl"]
            ?? Environment.GetEnvironmentVariable("Cbms__TokenEndpointUrl")
            ?? CbmsDefaults.SandboxTokenEndpointUrl;

        return new CbmsConnectionOptions(clientId, clientSecret, apiBaseUrl, tokenEndpointUrl);
    }
}

internal sealed record CbmsConnectionOptions(
    string ClientId,
    string ClientSecret,
    string ApiBaseUrl,
    string TokenEndpointUrl)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
