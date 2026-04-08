using System.Composition;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SEBT.Portal.StatePlugins.CO.Cbms;
using SEBT.Portal.StatePlugins.CO.CbmsApi;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Mocks;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.StatePlugins.CO;

[Export(typeof(IStatePlugin))]
[ExportMetadata("StateCode", "CO")]
public class ColoradoSummerEbtCaseService : ISummerEbtCaseService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ColoradoSummerEbtCaseService> _logger;
    private readonly HybridCache? _cache;

    private CbmsConnectionOptions? _cachedOptions;
    private CbmsSebtApiClient? _cachedClient;
    private readonly object _clientCacheLock = new();

    [ImportingConstructor]
    public ColoradoSummerEbtCaseService(
        [Import] IConfiguration configuration,
        [Import] ILoggerFactory loggerFactory,
        HybridCache? cache = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _configuration = configuration;
        _logger = loggerFactory.CreateLogger<ColoradoSummerEbtCaseService>();
        _cache = cache;
    }

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
            return null;
        }

        var normalizedPhone = PhoneNormalizer.Normalize(phoneNumber);
        if (string.IsNullOrEmpty(normalizedPhone))
        {
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
        catch (ErrorResponse ex)
        {
            _logger.LogWarning(ex, "CBMS GetAccountDetails failed with StatusCode: {StatusCode}; AdditionalData: {@AdditionalData}; ErrorDetails: {@ErrorDetails}",
                ex.ResponseStatusCode, ex.AdditionalData, ex.ErrorDetails);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CBMS GetAccountDetails failed for phone lookup.");
            throw;
        }
    }

    private CbmsSebtApiClient GetOrCreateClient(CbmsConnectionOptions options)
    {
        lock (_clientCacheLock)
        {
            if (_cachedOptions == options && _cachedClient != null)
                return _cachedClient;

            HttpMessageHandler? handler = null;
            var clientId = options.ClientId;
            var clientSecret = options.ClientSecret;

            if (options.UseMockResponses)
            {
                if (_cache == null)
                {
                    throw new InvalidOperationException(
                        "HybridCache must be registered in DI when Cbms:UseMockResponses is enabled. " +
                        "Ensure services.AddHybridCache() is called during service registration.");
                }

                clientId = "mock-client-id";
                clientSecret = "mock-client-secret";
                var dataStore = new MockCbmsDataStore(_cache);
                handler = new MockCbmsHttpHandler(dataStore);
            }

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
}
