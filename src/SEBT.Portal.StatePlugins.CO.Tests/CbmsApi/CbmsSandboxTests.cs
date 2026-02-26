using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;

namespace SEBT.Portal.StatePlugins.CO.Tests.CbmsApi;

/// <summary>
/// Integration tests that call the CBMS sandbox (UAT) API to verify the Kiota-generated client
/// works end-to-end: OAuth 2.0 authentication, request construction, and response parsing.
/// Tests skip gracefully when no credentials are configured.
/// </summary>
/// <remarks>
/// Assertions are intentionally loose — we're validating auth + serialization wiring,
/// not sandbox business data. Tighten assertions after seeing what the sandbox returns.
/// </remarks>
[Collection("CbmsSandbox")]
public class CbmsSandboxTests(CbmsSandboxFixture fixture)
{
    private const string SkipReason =
        "CBMS sandbox credentials not configured. " +
        "Set Cbms:SandboxClientId and Cbms:SandboxClientSecret via user-secrets or environment variables.";

    [SkippableFact]
    public async Task Ping_ReturnsSuccessResponse()
    {
        Skip.If(!fixture.CredentialsConfigured, SkipReason);

        var response = await fixture.Client!.Ping.GetAsync();

        Assert.NotNull(response);
    }

    [SkippableFact]
    public async Task CheckEnrollment_ReturnsResponse()
    {
        Skip.If(!fixture.CredentialsConfigured, SkipReason);

        var request = new List<CheckEnrollmentRequest>
        {
            new()
            {
                StdFirstName = "Test",
                StdLastName = "Student",
                StdDob = "2015-01-01",
                CbmsCsId = "C00001",
                StdSasId = "S00001",
                StdSchlCd = "SCH01",
                SebtYear = "2025",
                StdReqInd = "REQ-001",
            },
        };

        var response = await fixture.Client!.Sebt.CheckEnrollment.PostAsync(request);

        Assert.NotNull(response);
    }

    [SkippableFact]
    public async Task GetAccountDetails_ReturnsResponse()
    {
        Skip.If(!fixture.CredentialsConfigured, SkipReason);

        var request = new GetAccountDetailsRequest
        {
            PhnNm = "3035550199",
        };

        var response = await fixture.Client!.Sebt.GetAccountDetails.PostAsync(request);

        Assert.NotNull(response);
    }
}
