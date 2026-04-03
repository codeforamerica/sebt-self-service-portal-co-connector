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
        student.SebtAppId = 2001;
        student.StdFstNm = "Jane";
        student.StdLstNm = "Doe";
        student.StdDob = "2015-03-15";
        student.StdntEligSts = "Eligible";
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
        Assert.Equal("1001", @case.SummerEBTCaseID);
        Assert.Equal("2001", @case.ApplicationId);
        Assert.Equal("Jane", @case.ChildFirstName);
        Assert.Equal("Doe", @case.ChildLastName);
        Assert.Equal(new DateOnly(2015, 3, 15), @case.ChildDateOfBirth);
        Assert.Equal(ApplicationStatus.Approved, @case.ApplicationStatus);
        Assert.Equal("case-123", @case.EbtCaseNumber);
        Assert.Equal("4321", @case.EbtCardLastFour);
        Assert.Equal("active", @case.EbtCardStatus);
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

    [Theory]
    [InlineData("REQUESTED", CardStatus.Requested)]
    [InlineData("MAILED", CardStatus.Mailed)]
    [InlineData("ACTIVE", CardStatus.Active)]
    [InlineData("DEACTIVATED", CardStatus.Deactivated)]
    [InlineData("unknown", CardStatus.Unknown)]
    public void MapToHouseholdData_maps_card_status_in_applications(string ebtCardSts, CardStatus expected)
    {
        var student = CreateMinimalStudent();
        student.EbtCardSts = ebtCardSts;
        var response = new GetAccountDetailsResponse
        {
            StdntEnrollDtls = new List<GetAccountStudentDetail> { student }
        };
        var piiVisibility = new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false);

        var result = CbmsResponseMapper.MapToHouseholdData(response, "8185551234", piiVisibility);

        var app = Assert.Single(result.Applications);
        Assert.Equal(expected, app.CardStatus);
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
        var s2 = CreateMinimalStudent();
        s2.SebtAppId = 1001;
        s2.StdFstNm = "Child2";
        var s3 = CreateMinimalStudent();
        s3.SebtAppId = 2002;
        s3.StdFstNm = "Child3";
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

    private static GetAccountStudentDetail CreateMinimalStudent()
    {
        return new GetAccountStudentDetail
        {
            SebtChldId = 1,
            SebtAppId = 1,
            StdFstNm = "First",
            StdLstNm = "Last",
            StdDob = "2010-01-01"
        };
    }
}
