using System.Composition;
using Microsoft.Extensions.Configuration;
using Microsoft.Kiota.Abstractions;
using SEBT.Portal.StatePlugins.CO.Cbms;
using SEBT.Portal.StatePlugins.CO.CbmsApi;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.StatePlugins.CO;

/// <summary>
/// Colorado address updates via CBMS <c>update-std-dtls</c> (student mailing address).
/// Resolves the household with get-account-details using <see cref="PhoneNormalizer"/> on
/// <see cref="AddressUpdateRequest.HouseholdIdentifierValue"/>; uses the same normalization as <see cref="ColoradoSummerEbtCaseService"/> phone lookup.
/// When multiple children are on the account, CBMS receives one PATCH whose body is an array of update payloads
/// (same portal address mapped per student row from get-account-details).
/// Successful updates require CBMS <c>respCd</c> <c>200</c> (OpenAPI example) or <c>00</c> (observed UAT); other codes are failure.
/// </summary>
[Export(typeof(IAddressUpdateService))]
[Export(typeof(IStatePlugin))]
[ExportMetadata("StateCode", "CO")]
public class ColoradoAddressUpdateService : IAddressUpdateService
{
    private readonly IConfiguration? _configuration;
    private readonly HttpMessageHandler? _testHttpMessageHandler;

    [ImportingConstructor]
    public ColoradoAddressUpdateService([Import(AllowDefault = true)] IConfiguration? configuration = null)
    {
        _configuration = configuration;
        _testHttpMessageHandler = null;
    }

    internal ColoradoAddressUpdateService(IConfiguration? configuration, HttpMessageHandler? testHttpMessageHandler)
    {
        _configuration = configuration;
        _testHttpMessageHandler = testHttpMessageHandler;
    }

    /// <inheritdoc />
    public async Task<AddressUpdateResult> UpdateAddressAsync(
        AddressUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Address is null)
        {
            return AddressUpdateResult.PolicyRejected("INVALID_ADDRESS", "Address is required.");
        }

        if (request.HouseholdIdentifierValue is null)
        {
            return AddressUpdateResult.PolicyRejected(
                "INVALID_IDENTIFIER",
                "Colorado CBMS requires a valid US phone number in HouseholdIdentifierValue.");
        }

        if (!CbmsAddressUpdateMapper.TryValidatePortalAddress(request.Address, out var validationError))
        {
            return AddressUpdateResult.PolicyRejected("INVALID_ADDRESS", validationError ?? "Invalid address.");
        }

        var phone10 = PhoneNormalizer.Normalize(request.HouseholdIdentifierValue);
        if (string.IsNullOrEmpty(phone10))
        {
            return AddressUpdateResult.PolicyRejected(
                "INVALID_IDENTIFIER",
                "Colorado CBMS requires a valid US phone number in HouseholdIdentifierValue, same normalization as household lookup.");
        }

        var clientId = _configuration?["Cbms:ClientId"] ?? Environment.GetEnvironmentVariable("Cbms__ClientId");
        var clientSecret = _configuration?["Cbms:ClientSecret"] ?? Environment.GetEnvironmentVariable("Cbms__ClientSecret");
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return AddressUpdateResult.BackendError(
                "NOT_CONFIGURED",
                "CBMS credentials are not configured. Set Cbms:ClientId and Cbms:ClientSecret (or Cbms__* env vars).");
        }

        var apiBaseUrl = _configuration?["Cbms:ApiBaseUrl"]
            ?? Environment.GetEnvironmentVariable("Cbms__ApiBaseUrl");
        var tokenEndpointUrl = _configuration?["Cbms:TokenEndpointUrl"]
            ?? Environment.GetEnvironmentVariable("Cbms__TokenEndpointUrl");

        if (string.IsNullOrWhiteSpace(apiBaseUrl) || string.IsNullOrWhiteSpace(tokenEndpointUrl))
        {
            return AddressUpdateResult.BackendError(
                "NOT_CONFIGURED",
                "CBMS endpoint URLs are not configured. Set Cbms:ApiBaseUrl and Cbms:TokenEndpointUrl (or Cbms__* env vars). Do not rely on implicit sandbox defaults in production.");
        }

        var client = _testHttpMessageHandler != null
            ? CbmsSebtApiClientFactory.Create(clientId, clientSecret, apiBaseUrl, tokenEndpointUrl, _testHttpMessageHandler)
            : CbmsSebtApiClientFactory.Create(clientId, clientSecret, apiBaseUrl, tokenEndpointUrl);

        GetAccountDetailsResponse? accountResponse;
        try
        {
            accountResponse = await client.Sebt.GetAccountDetails.PostAsync(
                new GetAccountDetailsRequest { PhnNm = phone10 },
                cancellationToken: cancellationToken);
        }
        catch (ErrorResponse ex)
        {
            return MapErrorResponse(ex);
        }
        catch (ApiException ex)
        {
            return BackendErrorFromApiException(ex);
        }

        var students = accountResponse?.StdntEnrollDtls ?? [];
        var actionable = students
            .Select(row => (Row: row, Ids: CbmsGetAccountStudentDetailIds.Resolve(row)))
            .Where(x => CbmsGetAccountStudentDetailIds.CanBuildUpdatePayload(x.Ids))
            .ToList();

        if (actionable.Count == 0)
        {
            if (students.Count == 0)
            {
                return AddressUpdateResult.PolicyRejected(
                    "HOUSEHOLD_NOT_FOUND",
                    "CBMS get-account-details returned no enrollment rows for the household identifier used for lookup. " +
                    "Confirm this environment (UAT vs production) and that the guardian phone on the SEBT account matches lookup normalization, same as household by phone.");
            }

            return AddressUpdateResult.PolicyRejected(
                "HOUSEHOLD_NOT_FOUND",
                $"CBMS returned {students.Count} enrollment row(s), but none had usable sebtChldId or sebtAppId after parsing. " +
                CbmsGetAccountStudentDetailIds.FormatDiagnosticsHint(students[0]));
        }

        var updateBodies = actionable
            .Select(x => CbmsAddressUpdateMapper.ToUpdateStudentDetailsRequest(
                request.Address,
                x.Row,
                x.Ids.SebtChldId,
                x.Ids.SebtAppId))
            .ToList();

        try
        {
            var updateResponse = await client.Sebt.UpdateStdDtls.PatchAsync(updateBodies, cancellationToken: cancellationToken);
            if (updateResponse != null && IsCbmsUpdateSuccessCode(updateResponse.RespCd))
            {
                return AddressUpdateResult.Success();
            }

            var msg = updateResponse?.RespMsg ?? "CBMS returned an unexpected update response.";
            return AddressUpdateResult.BackendError("CBMS_UPDATE_FAILED", msg);
        }
        catch (ErrorResponse ex)
        {
            return MapErrorResponse(ex);
        }
        catch (ApiException ex)
        {
            return BackendErrorFromApiException(ex);
        }
    }

    /// <summary>Kiota uses the HTTP reason phrase (e.g. "Bad Request") as <see cref="ApiException.Message"/> — include status for clarity.</summary>
    private static AddressUpdateResult BackendErrorFromApiException(ApiException ex)
    {
        var status = ex.ResponseStatusCode;
        var msg = string.IsNullOrWhiteSpace(ex.Message) ? "(no error message)" : ex.Message.Trim();
        var detail = status is >= 100 and < 600 ? $"HTTP {(int)status}: {msg}" : msg;
        return AddressUpdateResult.BackendError("CBMS_ERROR", detail);
    }

    /// <summary>CBMS examples use "200"; live UAT has returned "00" with respMsg Success.</summary>
    internal static bool IsCbmsUpdateSuccessCode(string? respCd) =>
        string.Equals(respCd, "200", StringComparison.Ordinal)
        || string.Equals(respCd, "00", StringComparison.Ordinal);

    private static AddressUpdateResult MapErrorResponse(ErrorResponse ex)
    {
        var message = FormatErrorResponse(ex);
        if (ex.ResponseStatusCode == 404)
            return AddressUpdateResult.PolicyRejected("CBMS_404", message);
        return AddressUpdateResult.BackendError($"CBMS_{ex.ResponseStatusCode}", message);
    }

    /// <summary>
    /// CBMS 4xx/5xx responses deserialize to <see cref="ErrorResponse"/>; when the body omits
    /// <c>errorDetails</c>, Kiota leaves <see cref="ApiException.Message"/> as the HTTP reason phrase only (e.g. "Bad Request").
    /// Prefix with the status code (and surface correlation id) so the portal does not return a bare phrase as ProblemDetails detail.
    /// </summary>
    private static string FormatErrorResponse(ErrorResponse ex)
    {
        var status = ex.ResponseStatusCode;
        var prefix = status is >= 100 and < 600
            ? $"HTTP {status}"
            : "CBMS error";

        var fromDetails = ex.ErrorDetails?
            .Select(d => d.Message)
            .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m))
            ?.Trim();

        var body = !string.IsNullOrWhiteSpace(fromDetails)
            ? fromDetails
            : (string.IsNullOrWhiteSpace(ex.Message) ? "(no message)" : ex.Message.Trim());

        var msg = $"{prefix}: {body}";
        if (!string.IsNullOrWhiteSpace(ex.CorrelationId))
            msg += $" (correlationId: {ex.CorrelationId})";

        return msg;
    }
}
