using SEBT.Portal.StatePlugins.CO;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.StatePlugins.CO.Tests.CbmsApi;

/// <summary>
/// Opt-in live verification against CBMS UAT: OAuth, get-account-details, then PATCH update-std-dtls
/// with <c>reqNewCard = "Y"</c>. Uses the same credential flow as
/// <see cref="CbmsSandboxFixture"/>. Skips when no credentials or when the sandbox phone does not
/// resolve to a household (UAT data changes — no household means the happy path can't be exercised).
/// </summary>
[Collection("CbmsSandbox")]
public class ColoradoCardReplacementServiceSandboxTests(CbmsSandboxFixture fixture)
{
    private const string SkipReason =
        "CBMS sandbox credentials not configured. " +
        "Set Cbms:ClientId and Cbms:ClientSecret via user-secrets or environment variables.";

    [SkippableFact]
    public async Task RequestCardReplacementAsync_hits_live_UAT_pipeline()
    {
        Skip.If(!fixture.CredentialsConfigured, SkipReason);
        Skip.If(fixture.ColoradoCbmsConfiguration is null, SkipReason);

        var service = new ColoradoCardReplacementService(fixture.ColoradoCbmsConfiguration);
        var result = await service.RequestCardReplacementAsync(new CardReplacementRequest
        {
            HouseholdIdentifierValue = "8185558437",
            CaseIds = ["0"],
            Reason = CardReplacementReason.Unspecified
        });

        if (!result.IsSuccess && result.IsPolicyRejection && result.ErrorCode == "HOUSEHOLD_NOT_FOUND")
        {
            Skip.If(
                true,
                "No sandbox enrollment for phone 8185558437 — use a HouseholdIdentifierValue + CaseIds that get-account-details resolves.");
        }

        if (!result.IsSuccess && result.IsPolicyRejection && result.ErrorCode == "CASES_NOT_FOUND")
        {
            Skip.If(
                true,
                "Phone resolved, but placeholder CaseIds [\"0\"] did not match any sebtChldCwin. " +
                "Replace with a real cross-year cwin from the UAT enrollment data to exercise the PATCH path.");
        }

        Assert.True(result.IsSuccess, $"{result.ErrorCode}: {result.ErrorMessage}");
    }
}
