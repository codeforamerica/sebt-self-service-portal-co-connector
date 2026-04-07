using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;
using SEBT.Portal.StatesPlugins.Interfaces.Data.Cases;
using HouseholdAddress = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.Address;

namespace SEBT.Portal.StatePlugins.CO.Cbms;

/// <summary>
/// Maps CBMS Get Account Details response to the portal's HouseholdData model.
/// </summary>
/// <remarks>
/// <c>sebtAppSts</c> and <c>ebtCardSts</c> are interpreted as the full-word values shown in the CBMS OpenAPI examples (case-insensitive).
/// </remarks>
internal static class CbmsResponseMapper
{
    public static HouseholdData MapToHouseholdData(
        GetAccountDetailsResponse response,
        string queryPhone,
        PiiVisibility piiVisibility)
    {
        var students = response.StdntEnrollDtls ?? new List<GetAccountStudentDetail>();
        var first = students.FirstOrDefault();

        var household = new HouseholdData
        {
            Phone = piiVisibility.IncludePhone ? queryPhone : null,
            Email = piiVisibility.IncludeEmail && first != null ? first.GurdEmailAddr ?? string.Empty : string.Empty,
            AddressOnFile = first != null && piiVisibility.IncludeAddress ? MapAddress(first) : null,
            UserProfile = first != null ? new UserProfile
            {
                FirstName = first.GurdFstNm ?? string.Empty,
                LastName = first.GurdLstNm ?? string.Empty
            } : null,
            BenefitIssuanceType = BenefitIssuanceType.SummerEbt,
            SummerEbtCases = BuildCases(students, piiVisibility),
            Applications = BuildApplications(students)
        };

        return household;
    }

    private static string? FormatPostalCode(string? zip, string? zip4)
    {
        if (string.IsNullOrWhiteSpace(zip)) return null;
        return string.IsNullOrWhiteSpace(zip4) ? zip : $"{zip}-{zip4}";
    }

    private static HouseholdAddress? MapAddress(GetAccountStudentDetail s)
    {
        if (string.IsNullOrEmpty(s.AddrLn1) && string.IsNullOrEmpty(s.Cty))
            return null;
        return new HouseholdAddress
        {
            StreetAddress1 = s.AddrLn1,
            StreetAddress2 = s.AddrLn2,
            City = s.Cty,
            State = s.StaCd,
            PostalCode = FormatPostalCode(s.Zip, s.Zip4)
        };
    }

    private static SummerEbtCase MapToSummerEbtCase(GetAccountStudentDetail s, PiiVisibility piiVisibility)
    {
        return new SummerEbtCase
        {
            SummerEBTCaseID = s.SebtChldId?.ToString(),
            ApplicationId = EligibilitySourceClassifier.IsApplicationBased(s.EligSrc)
                ? s.SebtAppId?.ToString() : null,
            ApplicationStudentId = EligibilitySourceClassifier.IsApplicationBased(s.EligSrc)
                ? s.SebtChldId?.ToString() : null,
            ChildFirstName = s.StdFstNm ?? string.Empty,
            ChildLastName = s.StdLstNm ?? string.Empty,
            ChildDateOfBirth = ParseDateOnly(s.StdDob) ?? DateOnly.MinValue,
            HouseholdType = "SEBT",
            EligibilityType = s.StdntEligSts ?? string.Empty,
            ApplicationStatus = MapApplicationStatus(s.SebtAppSts),
            MailingAddress = piiVisibility.IncludeAddress ? MapAddress(s) : null,
            EbtCaseNumber = s.CbmsCsId,
            EbtCardLastFour = s.EbtCardLastFour,
            EbtCardStatus = s.EbtCardSts,
            EbtCardIssueDate = ParseDateOnly(s.CardIssDt),
            EbtCardBalance = s.CardBal.HasValue ? (decimal)s.CardBal.Value : null,
            BenefitAvailableDate = ParseDateOnly(s.BenAvalDt),
            BenefitExpirationDate = ParseDateOnly(s.BenExpDt),
            EligibilitySource = s.EligSrc,
            IssuanceType = IssuanceType.SummerEbt,  // CO does not co-load cards
        };
    }

    /// <summary>
    /// Builds the Cases collection. A child is a case if:
    /// - Auto-eligible (EligSrc = DIRC or CDE) — always a case
    /// - Unknown EligSrc (null/empty/unrecognized) — treated as auto-eligible
    /// - Application-based (EligSrc = CBMS or PK) AND approved
    /// </summary>
    private static List<SummerEbtCase> BuildCases(
        List<GetAccountStudentDetail> students,
        PiiVisibility piiVisibility)
    {
        return students
            .Where(s => !EligibilitySourceClassifier.IsApplicationBased(s.EligSrc)
                      || MapApplicationStatus(s.SebtAppSts) == ApplicationStatus.Approved)
            .Select(s => MapToSummerEbtCase(s, piiVisibility))
            .ToList();
    }

    /// <summary>
    /// Builds the Applications collection. Only rows where EligSrc indicates
    /// an actual application was submitted (CBMS or PK), grouped by SebtAppId.
    /// </summary>
    private static List<Application> BuildApplications(List<GetAccountStudentDetail> students)
    {
        var applicationRows = students
            .Where(s => EligibilitySourceClassifier.IsApplicationBased(s.EligSrc))
            .Where(s => s.SebtAppId != null)
            .GroupBy(s => s.SebtAppId!);

        return applicationRows.Select(g =>
        {
            var first = g.First();
            return new Application
            {
                ApplicationNumber = first.SebtAppId.ToString(),
                ApplicationStatus = MapApplicationStatus(first.SebtAppSts),
                Children = g.Select(c => new Child
                {
                    FirstName = c.StdFstNm ?? string.Empty,
                    LastName = c.StdLstNm ?? string.Empty,
                    Status = MapApplicationStatus(c.SebtAppSts)
                }).ToList()
            };
        }).ToList();
    }

    private static ApplicationStatus MapApplicationStatus(string? sebtAppSts)
    {
        if (string.IsNullOrEmpty(sebtAppSts)) return ApplicationStatus.Unknown;
        return sebtAppSts.ToUpperInvariant() switch
        {
            "PENDING" => ApplicationStatus.Pending,
            "APPROVED" => ApplicationStatus.Approved,
            "DENIED" => ApplicationStatus.Denied,
            "UNDER REVIEW" => ApplicationStatus.UnderReview,
            "CANCELLED" => ApplicationStatus.Cancelled,
            _ => ApplicationStatus.Unknown
        };
    }

    private static CardStatus MapCardStatus(string? ebtCardSts)
    {
        if (string.IsNullOrEmpty(ebtCardSts)) return CardStatus.Unknown;
        return ebtCardSts.ToUpperInvariant() switch
        {
            "REQUESTED" => CardStatus.Requested,
            "MAILED" => CardStatus.Mailed,
            "ACTIVE" => CardStatus.Active,
            "DEACTIVATED" => CardStatus.Deactivated,
            _ => CardStatus.Unknown
        };
    }

    private static DateOnly? ParseDateOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateOnly.TryParse(value, out var d) ? d : null;
    }

    private static DateTime? ParseDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTime.TryParse(value, out var dt) ? dt : null;
    }
}
