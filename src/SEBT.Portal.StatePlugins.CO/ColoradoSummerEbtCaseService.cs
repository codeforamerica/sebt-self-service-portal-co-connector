using System.Composition;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Data.Cases;

namespace SEBT.Portal.StatePlugins.CO;

[Export(typeof(IStatePlugin))]
[ExportMetadata("StateCode", "CO")]
public class ColoradoSummerEbtCaseService : ISummerEbtCaseService
{
    public Task<IList<SummerEbtCase>> GetHouseholdCases()
    {
        throw ThrowHelper.CreateColoradoNotImplementedException();
    }
}