using Microsoft.Extensions.Configuration;
using SEBT.Portal.StatePlugins.CO.CbmsApi;

namespace SEBT.Portal.StatePlugins.CO.Tests.CbmsApi;

/// <summary>
/// Shared xUnit fixture that creates a <see cref="CbmsSebtApiClient"/> pointed at the
/// CBMS sandbox (UAT) environment. When no credentials are configured,
/// <see cref="CredentialsConfigured"/> is false and tests should skip gracefully.
/// When <c>Cbms:UseMockResponses</c> is true, uses mock responses from the API spec examples instead.
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
/// Enable mock responses (no real API calls): <c>Cbms:UseMockResponses=true</c> or <c>Cbms__UseMockResponses=true</c>
/// </remarks>
public class CbmsSandboxFixture : IAsyncLifetime
{
    /// <summary>The configured Kiota client, or null when no credentials or mocks are available.</summary>
    public CbmsSebtApiClient? Client { get; private set; }

    /// <summary>Whether tests can run (real sandbox credentials or mock responses configured).</summary>
    public bool CredentialsConfigured { get; private set; }

    /// <summary>Whether mock responses are being used instead of the real sandbox.</summary>
    public bool UseMockResponses { get; private set; }

    public Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<CbmsSandboxFixture>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var useMock = configuration["Cbms:UseMockResponses"]?.Equals("true", StringComparison.OrdinalIgnoreCase) == true
            || configuration["Cbms__UseMockResponses"]?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

        if (useMock)
        {
            CredentialsConfigured = true;
            UseMockResponses = true;
            Client = CbmsSebtApiClientFactory.Create(
                clientId: "mock-client-id",
                clientSecret: "mock-client-secret",
                CbmsDefaults.SandboxApiBaseUrl,
                CbmsDefaults.SandboxTokenEndpointUrl,
                new MockCbmsHttpHandler());
            return Task.CompletedTask;
        }

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
