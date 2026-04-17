using System.Globalization;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;

namespace SEBT.Portal.StatePlugins.CO.Cbms;

/// <summary>
/// Builds CBMS <see cref="UpdateStudentDetailsRequest"/> bodies for card-replacement calls.
/// The endpoint (<c>/sebt/update-std-dtls</c>) is shared with address update; setting
/// <c>reqNewCard</c> to <c>"Y"</c> flags the body as a card-issuance request. The <c>addr</c>
/// field is required by the spec, so the current student address is passed through unchanged
/// from the get-account-details row.
/// </summary>
internal static class CbmsCardReplacementMapper
{
    internal const string ReqNewCardYes = "Y";

    /// <summary>
    /// Builds a card-replacement PATCH body for one student.
    /// </summary>
    /// <param name="studentRow">Enrollment row from get-account-details (source of passthrough address + guardian info).</param>
    /// <param name="resolvedSebtChldId">
    /// Optional id from <see cref="CbmsGetAccountStudentDetailIds.Resolve"/> when CBMS used non-standard JSON keys.
    /// </param>
    /// <param name="resolvedSebtAppId">Optional resolved application id (same).</param>
    public static UpdateStudentDetailsRequest ToCardReplacementBody(
        GetAccountStudentDetail studentRow,
        string? resolvedSebtChldId = null,
        string? resolvedSebtAppId = null)
    {
        ArgumentNullException.ThrowIfNull(studentRow);

        return new UpdateStudentDetailsRequest
        {
            SebtChldId = resolvedSebtChldId ?? studentRow.SebtChldId?.ToString(CultureInfo.InvariantCulture),
            SebtAppId = resolvedSebtAppId ?? studentRow.SebtAppId?.ToString(CultureInfo.InvariantCulture),
            Addr = PassthroughAddress(studentRow),
            GurdFstNm = studentRow.GurdFstNm,
            GurdLstNm = studentRow.GurdLstNm,
            GurdEmailAddr = studentRow.GurdEmailAddr,
            ReqNewCard = ReqNewCardYes,
        };
    }

    private static Address PassthroughAddress(GetAccountStudentDetail studentRow) =>
        new()
        {
            AddrLn1 = studentRow.AddrLn1,
            AddrLn2 = studentRow.AddrLn2,
            Cty = studentRow.Cty,
            StaCd = studentRow.StaCd,
            Zip = studentRow.Zip,
            Zip4 = studentRow.Zip4,
        };
}
