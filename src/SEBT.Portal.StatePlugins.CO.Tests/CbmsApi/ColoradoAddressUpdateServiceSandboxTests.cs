using SEBT.Portal.StatePlugins.CO;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.StatePlugins.CO.Tests.CbmsApi;

/// <summary>
/// Opt-in live verification against CBMS UAT: OAuth, get-account-details, then PATCH update-std-dtls.
/// Run locally with user secrets (same as <see cref="CbmsSandboxFixture"/>), for example:
/// <c>dotnet test --filter "FullyQualifiedName~ColoradoAddressUpdateServiceSandboxTests"</c>.
/// </summary>
[Collection("CbmsSandbox")]
public class ColoradoAddressUpdateServiceSandboxTests(CbmsSandboxFixture fixture)
{
    private const string SkipReason =
        "CBMS sandbox credentials not configured. " +
        "Set Cbms:ClientId and Cbms:ClientSecret via user-secrets or environment variables.";

    /// <summary>
    /// Uses the same sample phone as <see cref="CbmsSandboxTests.GetAccountDetails_ReturnsResponse"/>.
    /// Skips when that phone returns no enrollments; fails on auth/config; asserts success when CBMS accepts the update.
    /// </summary>
    [SkippableFact]
    public async Task UpdateAddressAsync_hits_live_UAT_pipeline()
    {
        Skip.If(!fixture.CredentialsConfigured, SkipReason);
        Skip.If(fixture.ColoradoCbmsConfiguration is null, SkipReason);

        var service = new ColoradoAddressUpdateService(fixture.ColoradoCbmsConfiguration);
        var result = await service.UpdateAddressAsync(new AddressUpdateRequest
        {
            HouseholdIdentifierValue = "8185558437",
            Address = new Address
            {
                StreetAddress1 = "200 E Colfax Ave",
                StreetAddress2 = "Floor 1",
                City = "Denver",
                State = "CO",
                PostalCode = "80203",
            }
        });

        if (!result.IsSuccess && result.IsPolicyRejection && result.ErrorCode == "HOUSEHOLD_NOT_FOUND")
        {
            Skip.If(
                true,
                "No sandbox enrollment for phone 8185558437 — use a HouseholdIdentifierValue that get-account-details resolves.");
        }

        Assert.True(result.IsSuccess, $"{result.ErrorCode}");
    }
}
