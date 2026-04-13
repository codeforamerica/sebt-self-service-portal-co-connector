using SEBT.Portal.StatePlugins.CO.Cbms;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Data.Cases;
using SEBT.Portal.StatesPlugins.Interfaces.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms;

public class CbmsResponseMapperTests
{
    [Fact]
    public void MapToHouseholdData_empty_students_returns_household_with_empty_cases()
    {
        var response = new GetAccountDetailsResponse { StdntEnrollDtls = new List<GetAccountStudentDetail>() };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: true);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        Assert.NotNull(result);
        Assert.Equal("8185551234", result.Phone);
        Assert.Empty(result.SummerEbtCases);
        Assert.Empty(result.Applications);
        Assert.Null(result.AddressOnFile);
        Assert.Null(result.UserProfile);
    }

    [Fact]
    public void MapToHouseholdData_null_students_treats_as_empty()
    {
        var response = new GetAccountDetailsResponse { StdntEnrollDtls = null };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        Assert.NotNull(result);
        Assert.Empty(result.SummerEbtCases);
    }

    [Fact]
    public void MapToHouseholdData_respects_PiiVisibility_IncludePhone_false()
    {
        var student = CreateMinimalStudent();
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        Assert.Null(result.Phone);
    }

    [Fact]
    public void MapToHouseholdData_respects_PiiVisibility_IncludeAddress_false()
    {
        var student = CreateMinimalStudent();
        student.AddrLn1 = "123 Main St";
        student.Cty = "Denver";
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: true, IncludePhone: true);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        Assert.Null(result.AddressOnFile);
        Assert.All(result.SummerEbtCases, c => Assert.Null(c.MailingAddress));
    }

    [Fact]
    public void MapToHouseholdData_maps_student_to_summer_ebt_case()
    {
        var student = CreateMinimalStudent();
        student.SebtChldId = 1001;
        student.SebtChldCwin = 5001001;
        student.SebtAppId = 2001;
        student.StdFstNm = "Jane";
        student.StdLstNm = "Doe";
        student.StdDob = "2015-03-15";
        student.StdntEligSts = "Eligible";
        student.EligSrc = "CBMS";
        student.SebtAppSts = "approved";
        student.CbmsCsId = "case-123";
        student.EbtCardLastFour = "4321";
        student.EbtCardSts = "active";
        student.BenAvalDt = "2025-06-01";
        student.BenExpDt = "2025-08-31";
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: true);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        var @case = Assert.Single(result.SummerEbtCases);
        Assert.Equal("5001001", @case.SummerEBTCaseID);
        Assert.Equal("2001", @case.ApplicationId);
        Assert.Equal("Jane", @case.ChildFirstName);
        Assert.Equal("Doe", @case.ChildLastName);
        Assert.Equal(new DateOnly(2015, 3, 15), @case.ChildDateOfBirth);
        Assert.Equal(ApplicationStatus.Approved, @case.ApplicationStatus);
        Assert.Equal("case-123", @case.EbtCaseNumber);
        Assert.Equal("4321", @case.EbtCardLastFour);
        Assert.Equal("Active", @case.EbtCardStatus);
    }

    [Theory]
    [InlineData("PENDING", ApplicationStatus.Pending)]
    [InlineData("APPROVED", ApplicationStatus.Approved)]
    [InlineData("DENIED", ApplicationStatus.Denied)]
    [InlineData("CANCELLED", ApplicationStatus.Cancelled)]
    [InlineData("UNDER REVIEW", ApplicationStatus.UnderReview)]
    [InlineData("unknown", ApplicationStatus.Unknown)]
    public void MapToHouseholdData_maps_application_status(string sebtAppSts, ApplicationStatus expected)
    {
        var student = CreateMinimalStudent();
        student.SebtAppSts = sebtAppSts;
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        var @case = Assert.Single(result.SummerEbtCases);
        Assert.Equal(expected, @case.ApplicationStatus);
    }

    [Fact]
    public void MapToHouseholdData_MapAddress_returns_null_when_AddrLn1_and_Cty_both_empty()
    {
        var student = CreateMinimalStudent();
        student.AddrLn1 = null;
        student.AddrLn2 = null;
        student.Cty = null;
        student.StaCd = null;
        student.Zip = null;
        student.Zip4 = null;
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: true, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        Assert.Null(result.AddressOnFile);
    }

    [Fact]
    public void MapToHouseholdData_FormatPostalCode_returns_null_when_zip_null_even_if_zip4_present()
    {
        var student = CreateMinimalStudent();
        student.AddrLn1 = "123 Main St";
        student.Cty = "Denver";
        student.Zip = null;
        student.Zip4 = "1234";
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: true, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        Assert.NotNull(result.AddressOnFile);
        Assert.Null(result.AddressOnFile.PostalCode);
    }

    [Fact]
    public void MapToHouseholdData_builds_applications_grouped_by_app_id()
    {
        var s1 = CreateMinimalStudent();
        s1.SebtAppId = 1001;
        s1.StdFstNm = "Child1";
        s1.EligSrc = "CBMS";
        var s2 = CreateMinimalStudent();
        s2.SebtAppId = 1001;
        s2.StdFstNm = "Child2";
        s2.EligSrc = "CBMS";
        var s3 = CreateMinimalStudent();
        s3.SebtAppId = 2002;
        s3.StdFstNm = "Child3";
        s3.EligSrc = "PK";
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { s1, s2, s3 }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        Assert.Equal(2, result.Applications.Count);
        var app1 = result.Applications.First(a => a.ApplicationNumber == "1001");
        Assert.Equal(2, app1.Children.Count);
        var app2 = result.Applications.First(a => a.ApplicationNumber == "2002");
        Assert.Single(app2.Children);
    }

    [Fact]
    public void MapToHouseholdData_auto_eligible_child_goes_to_cases_only()
    {
        var student = CreateMinimalStudent();
        student.EligSrc = "DIRC";
        student.StdFstNm = "AutoChild";
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        Assert.Single(result.SummerEbtCases);
        Assert.Equal("AutoChild", result.SummerEbtCases[0].ChildFirstName);
        Assert.Empty(result.Applications);
    }

    [Fact]
    public void MapToHouseholdData_application_child_pending_goes_to_applications_only()
    {
        var student = CreateMinimalStudent();
        student.EligSrc = "CBMS";
        student.SebtAppSts = "PENDING";
        student.StdFstNm = "AppChild";
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        Assert.Empty(result.SummerEbtCases);
        var app = Assert.Single(result.Applications);
        var child = Assert.Single(app.Children);
        Assert.Equal("AppChild", child.FirstName);
        Assert.Equal(ApplicationStatus.Pending, app.ApplicationStatus);
        Assert.Equal(IssuanceType.SummerEbt, app.IssuanceType);
    }

    [Fact]
    public void MapToHouseholdData_approved_application_child_goes_to_both_collections()
    {
        var student = CreateMinimalStudent();
        student.EligSrc = "CBMS";
        student.SebtAppSts = "APPROVED";
        student.StdFstNm = "ApprovedChild";
        student.EbtCardLastFour = "9999";
        student.EbtCardSts = "ACTIVE";
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        var caseRecord = Assert.Single(result.SummerEbtCases);
        Assert.Equal("ApprovedChild", caseRecord.ChildFirstName);
        Assert.Equal("9999", caseRecord.EbtCardLastFour);

        var app = Assert.Single(result.Applications);
        var child = Assert.Single(app.Children);
        Assert.Equal("ApprovedChild", child.FirstName);
        Assert.Equal(ApplicationStatus.Approved, app.ApplicationStatus);
        Assert.Equal(IssuanceType.SummerEbt, app.IssuanceType);
    }

    [Fact]
    public void MapToHouseholdData_unknown_eligsrc_treated_as_case()
    {
        var student = CreateMinimalStudent();
        student.EligSrc = "SOMETHING_NEW";
        student.StdFstNm = "UnknownChild";
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        Assert.Single(result.SummerEbtCases);
        Assert.Equal("UnknownChild", result.SummerEbtCases[0].ChildFirstName);
        Assert.Empty(result.Applications);
    }

    [Fact]
    public void MapToHouseholdData_DIRC_auto_eligible_sets_household_benefit_issuance_and_case()
    {
        var student = CreateMinimalStudent();
        student.EligSrc = "DIRC";
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        Assert.Equal(BenefitIssuanceType.SummerEbt, result.BenefitIssuanceType);
        Assert.Single(result.SummerEbtCases);
    }

    [Theory]
    [InlineData("ACTIVE", "Active")]
    [InlineData("REQUESTED", "Requested")]
    [InlineData("MAILED", "Mailed")]
    [InlineData("DEACTIVATED", "Deactivated")]
    [InlineData("", "Unknown")]
    [InlineData(null, "Unknown")]
    public void MapToHouseholdData_maps_card_status_through_MapCardStatus(string? ebtCardSts, string expected)
    {
        var student = CreateMinimalStudent();
        student.EbtCardSts = ebtCardSts;
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        var caseRecord = Assert.Single(result.SummerEbtCases);
        Assert.Equal(expected, caseRecord.EbtCardStatus);
    }

    [Fact]
    public void MapToHouseholdData_auto_eligible_case_has_no_application_reference()
    {
        var student = CreateMinimalStudent();
        student.EligSrc = "DIRC";
        student.SebtAppId = 5001;
        student.SebtChldId = 9001;
        student.SebtChldCwin = 7009001;
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        var caseRecord = Assert.Single(result.SummerEbtCases);
        Assert.Null(caseRecord.ApplicationId);
        Assert.Null(caseRecord.ApplicationStudentId);
        Assert.Equal("7009001", caseRecord.SummerEBTCaseID);
    }

    private static GetAccountStudentDetail CreateMinimalStudent()
    {
        return new GetAccountStudentDetail
        {
            SebtChldId = 1,
            SebtChldCwin = 100001,
            SebtAppId = 1,
            StdFstNm = "First",
            StdLstNm = "Last",
            StdDob = "2010-01-01",
            EligSrc = "DIRC"
        };
    }
}
