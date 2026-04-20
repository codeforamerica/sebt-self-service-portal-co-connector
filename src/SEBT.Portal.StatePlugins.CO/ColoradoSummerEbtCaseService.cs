using System.Composition;
using System.Diagnostics;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SEBT.Portal.StatePlugins.CO.Cbms;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.StatePlugins.CO;

[Export(typeof(IStatePlugin))]
[ExportMetadata("StateCode", "CO")]
public class ColoradoSummerEbtCaseService : ColoradoCbmsServiceBase, ISummerEbtCaseService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ColoradoSummerEbtCaseService> _logger;
 
    [ImportingConstructor]
    public ColoradoSummerEbtCaseService(
        [Import] IConfiguration configuration,
        [Import] ILoggerFactory loggerFactory,
        HybridCache? cache = null)
        : base(cache,  loggerFactory.CreateLogger<ColoradoSummerEbtCaseService>())
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _configuration = configuration;
        _logger = loggerFactory.CreateLogger<ColoradoSummerEbtCaseService>();
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

            _logger.LogInformation("CBMS GetAccountDetails: starting request (POST /sebt/get-account-details)");
            var sw = Stopwatch.StartNew();
            var response = await client.Sebt.GetAccountDetails.PostAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
            sw.Stop();

            var rowCount = response?.StdntEnrollDtls?.Count ?? 0;
            _logger.LogInformation(
                "CBMS GetAccountDetails: completed in {ElapsedMs}ms, returned {RowCount} enrollment row(s)",
                sw.ElapsedMilliseconds, rowCount);

            if (response?.StdntEnrollDtls == null || response.StdntEnrollDtls.Count == 0)
                return null;

            return CbmsResponseMapper.MapToHouseholdData(response, normalizedPhone, piiVisibility, _logger);
        }
        catch (ErrorResponse ex) when (ex.ResponseStatusCode == 404)
        {
            _logger.LogInformation("CBMS GetAccountDetails: returned 404 (no household found)");
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
}
