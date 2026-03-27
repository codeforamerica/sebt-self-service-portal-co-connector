using System.Composition;
using Microsoft.Extensions.Configuration;
using SEBT.Portal.StatePlugins.CO.CbmsApi;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Data.Cases;
using SEBT.Portal.StatesPlugins.Interfaces.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;
using PluginAddress = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.Address;

namespace SEBT.Portal.StatePlugins.CO;

/// <summary>
/// Colorado implementation of the Summer EBT case service.
/// Looks up household data via the CBMS API using the guardian's phone number.
/// </summary>
[Export(typeof(IStatePlugin))]
[ExportMetadata("StateCode", "CO")]
public class ColoradoSummerEbtCaseService : ISummerEbtCaseService
{
    private const string ClientIdConfigKey = "Cbms:ClientId";
    private const string ClientSecretConfigKey = "Cbms:ClientSecret";
    private const string ApiBaseUrlConfigKey = "Cbms:ApiBaseUrl";
    private const string TokenEndpointUrlConfigKey = "Cbms:TokenEndpointUrl";

    private readonly IConfiguration? _configuration;

    /// <summary>
    /// Initializes the service. The host should supply <paramref name="configuration"/> so CBMS
    /// credentials are read from <c>Cbms:ClientId</c>, <c>Cbms:ClientSecret</c>,
    /// <c>Cbms:ApiBaseUrl</c>, and <c>Cbms:TokenEndpointUrl</c> (or matching env vars).
    /// </summary>
    /// <param name="configuration">Optional. The application configuration; when null, only env vars are checked.</param>
    [ImportingConstructor]
    public ColoradoSummerEbtCaseService([Import(AllowDefault = true)] IConfiguration? configuration = null)
    {
        _configuration = configuration;
    }

    public Task<IList<SummerEbtCase>> GetHouseholdCases()
    {
        throw ThrowHelper.CreateColoradoNotImplementedException();
    }

    /// <inheritdoc />
    /// <remarks>Colorado does not support email-based household lookup. Use <see cref="GetHouseholdByGuardianPhoneAsync"/> instead.</remarks>
    public Task<HouseholdData?> GetHouseholdByGuardianEmailAsync(
        string guardianEmail,
        PiiVisibility piiVisibility,
        IdentityAssuranceLevel ial,
        CancellationToken cancellationToken = default)
    {
        throw ThrowHelper.CreateColoradoNotImplementedException();
    }

    /// <inheritdoc />
    public async Task<HouseholdData?> GetHouseholdByGuardianPhoneAsync(
        string guardianPhone,
        PiiVisibility piiVisibility,
        IdentityAssuranceLevel ial,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(guardianPhone);
        ArgumentNullException.ThrowIfNull(piiVisibility);

        var client = CreateApiClient();
        var normalizedPhone = NormalizePhone(guardianPhone);

        var request = new GetAccountDetailsRequest { PhnNm = normalizedPhone };
        var response = await client.Sebt.GetAccountDetails.PostAsync(request, cancellationToken: cancellationToken);

        var students = response?.StdntEnrollDtls;
        if (students == null || students.Count == 0)
        {
            return null;
        }

        return MapToHouseholdData(guardianPhone, students, piiVisibility.IncludeAddress);
    }

    private CbmsSebtApiClient CreateApiClient()
    {
        var clientId = _configuration?[ClientIdConfigKey]
            ?? Environment.GetEnvironmentVariable("Cbms__ClientId");
        var clientSecret = _configuration?[ClientSecretConfigKey]
            ?? Environment.GetEnvironmentVariable("Cbms__ClientSecret");
        var apiBaseUrl = _configuration?[ApiBaseUrlConfigKey]
            ?? Environment.GetEnvironmentVariable("Cbms__ApiBaseUrl");
        var tokenEndpointUrl = _configuration?[TokenEndpointUrlConfigKey]
            ?? Environment.GetEnvironmentVariable("Cbms__TokenEndpointUrl");

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException(
                "CBMS credentials are not configured. Set Cbms:ClientId and Cbms:ClientSecret in appsettings or via Cbms__ClientId / Cbms__ClientSecret environment variables.");
        }

        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            throw new InvalidOperationException(
                "CBMS API base URL is not configured. Set Cbms:ApiBaseUrl in appsettings or the Cbms__ApiBaseUrl environment variable.");
        }

        if (string.IsNullOrWhiteSpace(tokenEndpointUrl))
        {
            throw new InvalidOperationException(
                "CBMS token endpoint URL is not configured. Set Cbms:TokenEndpointUrl in appsettings or the Cbms__TokenEndpointUrl environment variable.");
        }

        return CbmsSebtApiClientFactory.Create(clientId, clientSecret, apiBaseUrl, tokenEndpointUrl);
    }

    /// <summary>
    /// Strips all non-digit characters from the phone number. CBMS expects digits only (e.g. "8005551234").
    /// </summary>
    private static string NormalizePhone(string phone)
    {
        return new string(phone.Where(char.IsDigit).ToArray());
    }

    private static HouseholdData MapToHouseholdData(
        string guardianPhone,
        IList<GetAccountStudentDetail> students,
        bool includeAddress)
    {
        var applications = students
            .GroupBy(s => s.SebtAppId ?? string.Empty)
            .Select(g => MapToApplication(g.ToList()))
            .ToList();

        PluginAddress? address = null;
        if (includeAddress)
        {
            var firstWithAddress = students.FirstOrDefault(s =>
                !string.IsNullOrWhiteSpace(s.AddrLn1) || !string.IsNullOrWhiteSpace(s.Cty));

            if (firstWithAddress != null)
            {
                address = new PluginAddress
                {
                    StreetAddress1 = firstWithAddress.AddrLn1,
                    StreetAddress2 = firstWithAddress.AddrLn2,
                    City = firstWithAddress.Cty,
                    State = firstWithAddress.StaCd,
                    PostalCode = BuildPostalCode(firstWithAddress.Zip, firstWithAddress.Zip4)
                };
            }
        }

        var first = students[0];

        return new HouseholdData
        {
            Email = first.GurdEmailAddr ?? string.Empty,
            Phone = guardianPhone,
            Applications = applications,
            AddressOnFile = address,
            BenefitIssuanceType = BenefitIssuanceType.SummerEbt
        };
    }

    private static Application MapToApplication(List<GetAccountStudentDetail> students)
    {
        var first = students[0];

        return new Application
        {
            ApplicationNumber = first.SebtAppId,
            CaseNumber = first.CbmsCsId,
            ApplicationStatus = MapApplicationStatus(first.SebtAppSts),
            BenefitIssueDate = ParseDate(first.CardIssDt),
            BenefitExpirationDate = ParseDate(first.BenExpDt),
            Last4DigitsOfCard = first.EbtCardLastFour,
            CardStatus = MapCardStatus(first.EbtCardSts),
            IssuanceType = IssuanceType.SummerEbt,
            Children = students.Select(s => new Child
            {
                FirstName = s.StdFstNm ?? string.Empty,
                LastName = s.StdLstNm ?? string.Empty
            }).ToList()
        };
    }

    private static ApplicationStatus MapApplicationStatus(string? status)
    {
        return status?.ToUpperInvariant() switch
        {
            "APPROVED" => ApplicationStatus.Approved,
            "DENIED" => ApplicationStatus.Denied,
            "PENDING" => ApplicationStatus.Pending,
            "IN_PROGRESS" => ApplicationStatus.Pending,
            "UNDER_REVIEW" => ApplicationStatus.UnderReview,
            "CANCELLED" => ApplicationStatus.Cancelled,
            _ => ApplicationStatus.Unknown
        };
    }

    private static CardStatus MapCardStatus(string? status)
    {
        return status?.ToUpperInvariant() switch
        {
            "ACTIVE" => CardStatus.Active,
            "MAILED" => CardStatus.Mailed,
            "REQUESTED" => CardStatus.Requested,
            "ISSUED" => CardStatus.Processed,
            "DEACTIVATED" => CardStatus.Deactivated,
            _ => CardStatus.Unknown
        };
    }

    private static DateTime? ParseDate(string? dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString))
            return null;

        if (DateOnly.TryParseExact(dateString, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var date))
            return date.ToDateTime(TimeOnly.MinValue);

        return null;
    }

    private static string? BuildPostalCode(string? zip, string? zip4)
    {
        if (string.IsNullOrWhiteSpace(zip))
            return null;

        return string.IsNullOrWhiteSpace(zip4) ? zip : $"{zip}-{zip4}";
    }
}
