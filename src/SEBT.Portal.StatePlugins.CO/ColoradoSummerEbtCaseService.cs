using System.Composition;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SEBT.Portal.StatePlugins.CO.Cbms;
using SEBT.Portal.StatePlugins.CO.CbmsApi;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Mocks;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Data.Cases;
using SEBT.Portal.StatesPlugins.Interfaces.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.StatePlugins.CO;

[Export(typeof(IStatePlugin))]
[ExportMetadata("StateCode", "CO")]
public class ColoradoSummerEbtCaseService : ISummerEbtCaseService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ColoradoSummerEbtCaseService>? _logger;

    private CbmsConnectionOptions? _cachedOptions;
    private CbmsSebtApiClient? _cachedClient;
    private readonly object _clientCacheLock = new();

    [ImportingConstructor]
    public ColoradoSummerEbtCaseService(
        [Import] IConfiguration configuration,
        [Import(AllowDefault = true)] ILogger<ColoradoSummerEbtCaseService>? logger = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;
    }

    /// <summary>
    /// Returns Summer EBT cases for the household identified by the given ID.
    /// </summary>
    /// <param name="householdId">For CO, the guardian email (household identifier).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of Summer EBT cases, or empty when not found or not configured.</returns>
    public async Task<IList<SummerEbtCase>> GetHouseholdCasesAsync(
        string? householdId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(householdId))
        {
            _logger?.LogDebug("No household ID provided; returning empty cases.");
            return new List<SummerEbtCase>();
        }

        var piiVisibility = new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true);
        var household = await GetHouseholdByGuardianEmailAsync(
            householdId,
            piiVisibility,
            IdentityAssuranceLevel.None,
            cancellationToken).ConfigureAwait(false);

        return household?.SummerEbtCases ?? new List<SummerEbtCase>();
    }

    /// <summary>
    /// Returns Summer EBT cases for the current household.  
    /// Accepts Type 'Phone'; Email is not supported.
    /// <see cref="GetHouseholdCasesAsync"/> with the resolved email when available.
    /// </summary>
    /// <returns>Empty list when no household identifier is available.</returns>
    public Task<IList<SummerEbtCase>> GetHouseholdCases() =>
        GetHouseholdCasesAsync(householdId: null);

    /// <inheritdoc />
    public async Task<HouseholdData?> GetHouseholdByIdentifierAsync(
        HouseholdIdentifierType identifierType,
        string identifierValue,
        PiiVisibility piiVisibility,
        IdentityAssuranceLevel identityAssuranceLevel,
        CancellationToken cancellationToken = default)
    {
        if (identifierType == HouseholdIdentifierType.Email)
            return await GetHouseholdByGuardianEmailAsync(identifierValue, piiVisibility, identityAssuranceLevel, cancellationToken).ConfigureAwait(false);

        if (identifierType == HouseholdIdentifierType.Phone)
            return await GetHouseholdByPhoneAsync(identifierValue, piiVisibility, cancellationToken).ConfigureAwait(false);

        return null;
    }

    /// <inheritdoc />
    public Task<HouseholdData?> GetHouseholdByGuardianEmailAsync(
        string guardianEmail,
        PiiVisibility piiVisibility,
        IdentityAssuranceLevel identityAssuranceLevel,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug("CBMS does not support lookup by guardian email; returning null.");
        return Task.FromResult<HouseholdData?>(null);
    }

    private async Task<HouseholdData?> GetHouseholdByPhoneAsync(
        string phoneNumber,
        PiiVisibility piiVisibility,
        CancellationToken cancellationToken)
    {
        var options = CbmsOptionsHelper.GetCbmsOptions(_configuration);
        if (!options.IsConfigured)
        {
            _logger?.LogDebug("Cbms not configured; skipping phone lookup.");
            return null;
        }

        var normalizedPhone = NormalizePhone(phoneNumber);
        if (string.IsNullOrEmpty(normalizedPhone))
        {
            _logger?.LogWarning("Invalid or empty phone number for CBMS lookup.");
            return null;
        }

        try
        {
            var client = GetOrCreateClient(options);
            var request = new GetAccountDetailsRequest { PhnNm = normalizedPhone };
            var response = await client.Sebt.GetAccountDetails.PostAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response?.StdntEnrollDtls == null || response.StdntEnrollDtls.Count == 0)
                return null;

            return CbmsResponseMapper.MapToHouseholdData(response, normalizedPhone, piiVisibility);
        }
        catch (ErrorResponse ex) when (ex.ResponseStatusCode == 404)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "CBMS Get Account Details failed for phone lookup.");
            throw;
        }
    }

    private CbmsSebtApiClient GetOrCreateClient(CbmsConnectionOptions options)
    {
        lock (_clientCacheLock)
        {
            if (_cachedOptions == options && _cachedClient != null)
                return _cachedClient;

            var clientId = options.UseMockResponses ? "mock-client-id" : options.ClientId;
            var clientSecret = options.UseMockResponses ? "mock-client-secret" : options.ClientSecret;
            var handler = options.UseMockResponses
                ? new MockCbmsHttpHandler(return404ForGetAccountDetails: options.Return404ForGetAccountDetails)
                : null;

            _cachedClient = CbmsSebtApiClientFactory.Create(
                clientId,
                clientSecret,
                options.ApiBaseUrl,
                options.TokenEndpointUrl,
                handler);
            _cachedOptions = options;
            return _cachedClient;
        }
    }

    private static string? NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length >= 10 ? digits : null;
    }
}
