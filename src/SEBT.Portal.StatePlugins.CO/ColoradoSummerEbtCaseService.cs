using System.Composition;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Data.Cases;
using SEBT.Portal.StatesPlugins.Interfaces.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.StatePlugins.CO;

[Export(typeof(IStatePlugin))]
[ExportMetadata("StateCode", "CO")]
public class ColoradoSummerEbtCaseService : ISummerEbtCaseService
{
    public Task<IList<SummerEbtCase>> GetHouseholdCases()
    {
        throw ThrowHelper.CreateColoradoNotImplementedException();
    }

    public Task<HouseholdData?> GetHouseholdByGuardianEmailAsync(
        string guardianEmail, 
        PiiVisibility piiVisibility, 
        IdentityAssuranceLevel ial,
        CancellationToken cancellationToken = default)
    {
        throw ThrowHelper.CreateColoradoNotImplementedException();
    }
}