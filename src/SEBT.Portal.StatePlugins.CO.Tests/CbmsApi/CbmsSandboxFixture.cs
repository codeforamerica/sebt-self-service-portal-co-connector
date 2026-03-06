using Microsoft.Extensions.Configuration;
using SEBT.Portal.StatePlugins.CO.CbmsApi;

namespace SEBT.Portal.StatePlugins.CO.Tests.CbmsApi;

/// <summary>
/// Shared xUnit fixture that creates a <see cref="CbmsSebtApiClient"/> pointed at the
/// CBMS sandbox (UAT) environment. When no credentials are configured,
/// <see cref="CredentialsConfigured"/> is false and tests should skip gracefully.
/// </summary>
/// <remarks>
/// Configure credentials via:
/// <list type="bullet">
///   <item>User secrets:
///     <c>dotnet user-secrets set "Cbms:SandboxClientId" "&lt;id&gt;"</c> and
///     <c>dotnet user-secrets set "Cbms:SandboxClientSecret" "&lt;secret&gt;"</c>
///   </item>
///   <item>Environment variables:
///     <c>Cbms__SandboxClientId</c> and <c>Cbms__SandboxClientSecret</c>
///   </item>
/// </list>
/// </remarks>
public class CbmsSandboxFixture : IAsyncLifetime
{
    /// <summary>The configured Kiota client, or null when no credentials are available.</summary>
    public CbmsSebtApiClient? Client { get; private set; }

    /// <summary>Whether sandbox credentials were found in configuration.</summary>
    public bool CredentialsConfigured { get; private set; }

    public Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<CbmsSandboxFixture>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var clientId = configuration["Cbms:SandboxClientId"];
        var clientSecret = configuration["Cbms:SandboxClientSecret"];

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            CredentialsConfigured = false;
            return Task.CompletedTask;
        }

        CredentialsConfigured = true;
        Client = CbmsSebtApiClientFactory.Create(
            clientId,
            clientSecret,
            CbmsDefaults.SandboxApiBaseUrl,
            CbmsDefaults.SandboxTokenEndpointUrl);

        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition("CbmsSandbox")]
public class CbmsSandboxCollection : ICollectionFixture<CbmsSandboxFixture>
{
}
