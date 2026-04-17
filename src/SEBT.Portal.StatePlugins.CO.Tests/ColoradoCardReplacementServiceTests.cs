using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.StatePlugins.CO.Tests;

public class ColoradoCardReplacementServiceTests
{
    [Fact]
    public async Task RequestCardReplacementAsync_returns_backend_error_with_CO_NOT_IMPLEMENTED()
    {
        var service = new ColoradoCardReplacementService();
        var request = new CardReplacementRequest
        {
            HouseholdIdentifierValue = "5551234567",
            CaseIds = new List<string> { "SEBT-001" },
            Reason = CardReplacementReason.Unspecified,
        };

        var result = await service.RequestCardReplacementAsync(request);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsPolicyRejection);
        Assert.Equal("CO_NOT_IMPLEMENTED", result.ErrorCode);
    }
}
