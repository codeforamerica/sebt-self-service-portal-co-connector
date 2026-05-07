using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;

namespace SEBT.Portal.StatePlugins.CO.Cbms;

/// <summary>
/// Single owner of CBMS row-level filtering predicates used by write-path services.
/// </summary>
internal static class CbmsCaseFilters
{
    /// <summary>
    /// True when the row is a Denied Duplicate. CBMS encodes this as <c>stdntEligSts="DD"</c>.
    /// DD rows must not be exposed to the frontend or be acted on by the
    /// card-replacement write path.
    /// </summary>
    public static bool IsDeniedDuplicate(GetAccountStudentDetail row) =>
        row is not null
        && string.Equals(row.StdntEligSts, "DD", StringComparison.OrdinalIgnoreCase);
}
