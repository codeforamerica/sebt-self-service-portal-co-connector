using System.Composition;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.StatePlugins.CO;

/// <summary>
/// Colorado implementation of the card replacement service. Stub:
/// the CO card-issuance integration has not yet been wired — CBMS handles
/// case data but card issuance may live in a separate vendor system that
/// the team has not yet identified. Returning a backend error keeps the
/// portal end-to-end path exercised so the wiring becomes a connector-only
/// change once the integration shape is known.
/// </summary>
[Export(typeof(IStatePlugin))]
[ExportMetadata("StateCode", "CO")]
public class ColoradoCardReplacementService : ICardReplacementService
{
    public Task<CardReplacementResult> RequestCardReplacementAsync(
        CardReplacementRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            CardReplacementResult.BackendError(
                "CO_NOT_IMPLEMENTED",
                "CO card replacement integration is not yet wired."));
    }
}
