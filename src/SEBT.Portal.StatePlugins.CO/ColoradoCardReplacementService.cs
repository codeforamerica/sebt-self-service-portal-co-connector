using System.Composition;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Kiota.Abstractions;
using SEBT.Portal.StatePlugins.CO.Cbms;
using SEBT.Portal.StatePlugins.CO.CbmsApi;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.StatePlugins.CO;

/// <summary>
/// Colorado card-replacement via CBMS <c>update-std-dtls</c> with <c>reqNewCard = "Y"</c>.
/// Resolves the household with get-account-details using <see cref="PhoneNormalizer"/> on
/// <see cref="CardReplacementRequest.HouseholdIdentifierValue"/>, filters enrollment rows to
/// those whose <c>sebtChldCwin</c> matches the requested <see cref="CardReplacementRequest.CaseIds"/>,
/// then sends a single PATCH with one array element per matched student. The portal's
/// <c>SummerEBTCaseID</c> maps to CBMS <c>sebtChldCwin</c> (cross-year); the PATCH body uses the
/// per-year <c>sebtChldId</c> / <c>sebtAppId</c>.
/// Successful updates require CBMS <c>respCd</c> <c>200</c> (spec) or <c>00</c> (observed UAT).
/// </summary>
[Export(typeof(IStatePlugin))]
[ExportMetadata("StateCode", "CO")]
public class ColoradoCardReplacementService : ICardReplacementService
{
    private readonly IConfiguration? _configuration;
    private readonly ILogger _logger;
    private readonly HttpMessageHandler? _testHttpMessageHandler;

    private CbmsConnectionOptions? _cachedOptions;
    private CbmsSebtApiClient? _cachedClient;
    private readonly object _clientCacheLock = new();

    [ImportingConstructor]
    public ColoradoCardReplacementService(
        [Import(AllowDefault = true)] IConfiguration? configuration = null,
        [Import(AllowDefault = true)] ILoggerFactory? loggerFactory = null)
    {
        _configuration = configuration;
        _logger = loggerFactory?.CreateLogger<ColoradoCardReplacementService>() ?? NullLogger<ColoradoCardReplacementService>.Instance;
        _testHttpMessageHandler = null;
    }

    internal ColoradoCardReplacementService(IConfiguration? configuration, HttpMessageHandler? testHttpMessageHandler, ILogger? logger = null)
    {
        _configuration = configuration;
        _testHttpMessageHandler = testHttpMessageHandler;
        _logger = logger ?? NullLogger<ColoradoCardReplacementService>.Instance;
    }

    /// <inheritdoc />
    public async Task<CardReplacementResult> RequestCardReplacementAsync(
        CardReplacementRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.HouseholdIdentifierValue is null)
        {
            return CardReplacementResult.PolicyRejected(
                "INVALID_IDENTIFIER",
                "Colorado CBMS requires a valid US phone number in HouseholdIdentifierValue.");
        }

        if (request.CaseIds is null || request.CaseIds.Count == 0)
        {
            return CardReplacementResult.PolicyRejected(
                "INVALID_CASE_IDS",
                "At least one case id is required.");
        }

        var phone10 = PhoneNormalizer.Normalize(request.HouseholdIdentifierValue);
        if (string.IsNullOrEmpty(phone10))
        {
            return CardReplacementResult.PolicyRejected(
                "INVALID_IDENTIFIER",
                "Colorado CBMS requires a valid US phone number in HouseholdIdentifierValue, same normalization as household lookup.");
        }

        var options = CbmsOptionsHelper.GetCbmsOptions(_configuration);
        if (string.IsNullOrWhiteSpace(options.ClientId) || string.IsNullOrWhiteSpace(options.ClientSecret))
        {
            return CardReplacementResult.BackendError(
                "NOT_CONFIGURED",
                "CBMS credentials are not configured. Set Cbms:ClientId and Cbms:ClientSecret (or Cbms__* env vars).");
        }

        if (string.IsNullOrWhiteSpace(options.ApiBaseUrl) || string.IsNullOrWhiteSpace(options.TokenEndpointUrl))
        {
            return CardReplacementResult.BackendError(
                "NOT_CONFIGURED",
                "CBMS endpoint URLs are not configured. Set Cbms:ApiBaseUrl and Cbms:TokenEndpointUrl (or Cbms__* env vars).");
        }

        var client = GetOrCreateClient(options);

        GetAccountDetailsResponse? accountResponse;
        try
        {
            _logger.LogInformation("CBMS CardReplacement: fetching account details (POST /sebt/get-account-details)");
            var sw = Stopwatch.StartNew();
            accountResponse = await client.Sebt.GetAccountDetails.PostAsync(
                new GetAccountDetailsRequest { PhnNm = phone10 },
                cancellationToken: cancellationToken);
            sw.Stop();
            _logger.LogInformation(
                "CBMS CardReplacement: get-account-details completed in {ElapsedMs}ms, {RowCount} enrollment row(s)",
                sw.ElapsedMilliseconds, accountResponse?.StdntEnrollDtls?.Count ?? 0);
        }
        catch (ErrorResponse ex)
        {
            _logger.LogWarning("CBMS CardReplacement: get-account-details failed with StatusCode={StatusCode}", ex.ResponseStatusCode);
            return MapErrorResponse(ex);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning("CBMS CardReplacement: get-account-details failed with HTTP {StatusCode}", ex.ResponseStatusCode);
            return BackendErrorFromApiException(ex);
        }

        var students = accountResponse?.StdntEnrollDtls ?? [];
        if (students.Count == 0)
        {
            return CardReplacementResult.PolicyRejected(
                "HOUSEHOLD_NOT_FOUND",
                "CBMS get-account-details returned no enrollment rows for the household identifier.");
        }

        var requestedCwins = request.CaseIds.ToHashSet(StringComparer.Ordinal);
        var matched = students
            .Select(row => (Row: row, Cwin: row.SebtChldCwin?.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .Where(x => x.Cwin is not null && requestedCwins.Contains(x.Cwin))
            .Select(x => (x.Row, Ids: CbmsGetAccountStudentDetailIds.Resolve(x.Row)))
            .Where(x => CbmsGetAccountStudentDetailIds.CanBuildUpdatePayload(x.Ids))
            .ToList();

        if (matched.Count != request.CaseIds.Count)
        {
            return CardReplacementResult.PolicyRejected(
                "CASES_NOT_FOUND",
                $"Requested {request.CaseIds.Count} case(s), but only {matched.Count} matched usable CBMS enrollment row(s). " +
                "Portal case list may be stale; ask the user to refresh and retry.");
        }

        var updateBodies = matched
            .Select(x => CbmsCardReplacementMapper.ToCardReplacementBody(
                x.Row,
                x.Ids.SebtChldId,
                x.Ids.SebtAppId))
            .ToList();

        try
        {
            _logger.LogInformation(
                "CBMS CardReplacement: requesting new card for {StudentCount} student(s) (PATCH /sebt/update-std-dtls)",
                updateBodies.Count);
            var patchSw = Stopwatch.StartNew();
            var updateResponse = await client.Sebt.UpdateStdDtls.PatchAsync(updateBodies, cancellationToken: cancellationToken);
            patchSw.Stop();
            _logger.LogInformation(
                "CBMS CardReplacement: update-std-dtls completed in {ElapsedMs}ms, respCd={RespCd}",
                patchSw.ElapsedMilliseconds, updateResponse?.RespCd ?? "(null)");

            if (updateResponse != null && ColoradoAddressUpdateService.IsCbmsUpdateSuccessCode(updateResponse.RespCd))
            {
                return CardReplacementResult.Success();
            }

            var msg = updateResponse?.RespMsg ?? "CBMS returned an unexpected card-replacement response.";
            return CardReplacementResult.BackendError("CBMS_UPDATE_FAILED", msg);
        }
        catch (ErrorResponse ex)
        {
            _logger.LogWarning("CBMS CardReplacement: update-std-dtls failed with StatusCode={StatusCode}", ex.ResponseStatusCode);
            return MapErrorResponse(ex);
        }
        catch (ApiException ex)
        {
            _logger.LogWarning("CBMS CardReplacement: update-std-dtls failed with HTTP {StatusCode}", ex.ResponseStatusCode);
            return BackendErrorFromApiException(ex);
        }
    }

    private CbmsSebtApiClient GetOrCreateClient(CbmsConnectionOptions options)
    {
        lock (_clientCacheLock)
        {
            if (_cachedOptions == options && _cachedClient != null)
                return _cachedClient;

            _cachedClient = CbmsSebtApiClientFactory.Create(
                options.ClientId,
                options.ClientSecret,
                options.ApiBaseUrl,
                options.TokenEndpointUrl,
                _testHttpMessageHandler,
                _logger);
            _cachedOptions = options;
            return _cachedClient;
        }
    }

    /// <summary>Kiota uses the HTTP reason phrase (e.g. "Bad Request") as <see cref="ApiException.Message"/> — include status for clarity.</summary>
    private static CardReplacementResult BackendErrorFromApiException(ApiException ex)
    {
        var status = ex.ResponseStatusCode;
        var msg = string.IsNullOrWhiteSpace(ex.Message) ? "(no error message)" : ex.Message.Trim();
        var detail = status is >= 100 and < 600 ? $"HTTP {(int)status}: {msg}" : msg;
        return CardReplacementResult.BackendError("CBMS_ERROR", detail);
    }

    private static CardReplacementResult MapErrorResponse(ErrorResponse ex)
    {
        var message = FormatErrorResponse(ex);
        if (ex.ResponseStatusCode == 404)
            return CardReplacementResult.PolicyRejected("CBMS_404", message);
        return CardReplacementResult.BackendError($"CBMS_{ex.ResponseStatusCode}", message);
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
