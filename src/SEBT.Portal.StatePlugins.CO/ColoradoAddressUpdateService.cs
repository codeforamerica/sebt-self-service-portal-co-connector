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
/// Resolves the household with get-account-details using a normalized 10-digit phone in
/// <see cref="AddressUpdateRequest.HouseholdIdentifierValue"/>.
/// When multiple children are on the account, updates are rejected until the contract carries a disambiguator.
/// Successful updates require CBMS to return <c>respCd</c> "200" (string); other codes are treated as failure.
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
                "Colorado CBMS requires a 10-digit phone number in HouseholdIdentifierValue (optionally formatted).");
        }

        if (!CbmsAddressUpdateMapper.TryValidatePortalAddress(request.Address, out var validationError))
        {
            return AddressUpdateResult.PolicyRejected("INVALID_ADDRESS", validationError ?? "Invalid address.");
        }

        if (!TryNormalizePhoneNumber(request.HouseholdIdentifierValue, out var phone10))
        {
            return AddressUpdateResult.PolicyRejected(
                "INVALID_IDENTIFIER",
                "Colorado CBMS requires a 10-digit phone number in HouseholdIdentifierValue (optionally formatted).");
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
            return AddressUpdateResult.BackendError("CBMS_ERROR", ex.Message);
        }

        var students = accountResponse?.StdntEnrollDtls ?? [];
        var withChildId = students.Where(s => !string.IsNullOrWhiteSpace(s.SebtChldId)).ToList();

        if (withChildId.Count == 0)
        {
            return AddressUpdateResult.PolicyRejected(
                "HOUSEHOLD_NOT_FOUND",
                "No household record with a child identifier was found for this phone number.");
        }

        if (withChildId.Count > 1)
        {
            return AddressUpdateResult.PolicyRejected(
                "AMBIGUOUS_HOUSEHOLD",
                "More than one child is associated with this phone number; the portal must specify which student to update.");
        }

        var studentRow = withChildId[0];
        var updateBody = CbmsAddressUpdateMapper.ToUpdateStudentDetailsRequest(request.Address, studentRow);

        try
        {
            var updateResponse = await client.Sebt.UpdateStdDtls.PatchAsync(updateBody, cancellationToken: cancellationToken);
            if (updateResponse != null &&
                string.Equals(updateResponse.RespCd, "200", StringComparison.Ordinal))
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
            return AddressUpdateResult.BackendError("CBMS_ERROR", ex.Message);
        }
    }

    private static AddressUpdateResult MapErrorResponse(ErrorResponse ex)
    {
        var message = FormatErrorResponse(ex);
        if (ex.ResponseStatusCode == 404)
            return AddressUpdateResult.PolicyRejected("CBMS_404", message);
        return AddressUpdateResult.BackendError($"CBMS_{ex.ResponseStatusCode}", message);
    }

    private static string FormatErrorResponse(ErrorResponse ex)
    {
        var detail = ex.ErrorDetails?.FirstOrDefault()?.Message;
        return !string.IsNullOrWhiteSpace(detail) ? detail : ex.Message;
    }

    internal static bool TryNormalizePhoneNumber(string? value, out string phone10)
    {
        if (value is null)
        {
            phone10 = string.Empty;
            return false;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length == 11 && digits.StartsWith("1", StringComparison.Ordinal))
            digits = digits[1..];

        if (digits.Length == 10)
        {
            phone10 = digits;
            return true;
        }

        phone10 = string.Empty;
        return false;
    }
}
