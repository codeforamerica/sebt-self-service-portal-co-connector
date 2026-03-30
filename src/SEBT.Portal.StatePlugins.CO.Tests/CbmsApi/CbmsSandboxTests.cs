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
        "Set Cbms:ClientId and Cbms:ClientSecret via user-secrets or environment variables.";

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
                StdFirstName = "REON",
                StdLastName = "NEBADA",
                StdDob = "2010-07-13",
                CbmsCsId = "",
                StdSasId = "",
                StdSchlCd = "",
                SebtYear = "2025",
                StdReqInd = "1",
            },
        };

        var response = await fixture.Client!.Sebt.CheckEnrollment.PostAsync(request);

        Assert.NotNull(response);
    }

    [SkippableFact]
    public async Task GetAccountDetails_ReturnsValidResponse_WhenSandboxIsReachable()
    {
        Skip.If(!fixture.CredentialsConfigured, SkipReason);
        Skip.If(fixture.UseMockResponses, "Skipped when using mock responses; this test verifies the real sandbox response.");

        var request = new GetAccountDetailsRequest
        {
            PhnNm = "8185558437",
        };

        var response = await fixture.Client!.Sebt.GetAccountDetails.PostAsync(request);

        // Sandbox may return null or empty when no account exists for the phone
        if (response is null || response.StdntEnrollDtls is null || response.StdntEnrollDtls.Count == 0)
        {
            Skip.If(true, "Sandbox has no test data for phone 8185558437; cannot verify response structure.");
        }

        Assert.All(response!.StdntEnrollDtls!, student =>
        {
            Assert.NotNull(student);
            // Sandbox may return partial records; verify core identifiers when present
            if (!string.IsNullOrEmpty(student.SebtChldId))
                Assert.False(string.IsNullOrEmpty(student.SebtAppId), "Student with SebtChldId must have SebtAppId.");
            // When StdDob is present, it must be parseable as a date (YYYY-MM-DD per spec)
            if (!string.IsNullOrEmpty(student.StdDob))
                Assert.True(DateOnly.TryParse(student.StdDob, out _), $"StdDob must be a valid date, got: {student.StdDob}");
            // When date fields are present, they must be parseable
            if (!string.IsNullOrEmpty(student.BenAvalDt))
                Assert.True(DateOnly.TryParse(student.BenAvalDt, out _), $"BenAvalDt must be a valid date, got: {student.BenAvalDt}");
            if (!string.IsNullOrEmpty(student.BenExpDt))
                Assert.True(DateOnly.TryParse(student.BenExpDt, out _), $"BenExpDt must be a valid date, got: {student.BenExpDt}");
            // EbtCardLastFour when present should be 4 digits
            if (!string.IsNullOrEmpty(student.EbtCardLastFour))
                Assert.Matches("^[0-9]{4}$", student.EbtCardLastFour);
        });
    }
}
