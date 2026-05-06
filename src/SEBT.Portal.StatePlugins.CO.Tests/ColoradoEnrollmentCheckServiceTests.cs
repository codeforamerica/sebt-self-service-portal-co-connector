using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;

namespace SEBT.Portal.StatePlugins.CO.Tests;

public class ColoradoEnrollmentCheckServiceTests
{
    [Fact]
    public async Task CheckEnrollmentAsync_WhenRequestHasNoChildren_ReturnsEmptyResults()
    {
        var service = new ColoradoEnrollmentCheckService();
        var request = new EnrollmentCheckRequest
        {
            Children = new List<ChildCheckRequest>()
        };

        var result = await service.CheckEnrollmentAsync(request);

        Assert.NotNull(result);
        Assert.Empty(result.Results);
    }

    [Fact]
    public async Task CheckEnrollmentAsync_WhenNoApiConfiguration_ThrowsInvalidOperationException()
    {
        var service = new ColoradoEnrollmentCheckService();
        var request = new EnrollmentCheckRequest
        {
            Children = new List<ChildCheckRequest>
            {
                new ChildCheckRequest
                {
                    CheckId = Guid.NewGuid(),
                    FirstName = "Jane",
                    LastName = "Doe",
                    DateOfBirth = new DateOnly(2015, 3, 12),
                    SchoolName = "Lincoln Elementary"
                }
            }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CheckEnrollmentAsync(request));
    }

    [Theory]
    [InlineData("Y", EnrollmentStatus.Match)]
    [InlineData("y", EnrollmentStatus.Match)]
    [InlineData("N", EnrollmentStatus.NonMatch)]
    [InlineData("n", EnrollmentStatus.NonMatch)]
    [InlineData(null, EnrollmentStatus.NonMatch)]
    [InlineData("", EnrollmentStatus.NonMatch)]
    [InlineData("UNEXPECTED", EnrollmentStatus.NonMatch)]
    public void MapEnrollmentStatus_MapsSebtEligStsValues(string? status, EnrollmentStatus expected)
    {
        var result = ColoradoEnrollmentCheckService.MapEnrollmentStatus(status);

        Assert.Equal(expected, result);
    }

    // ---------------------------------------------------------------------------
    // CorrelateResults: response correlation by stdReqInd, threshold, tie-break
    //
    // Note on test surface choice (per plan.md fallback option): we test
    // CorrelateResults directly and the request-shape via BuildCbmsRequests rather
    // than spinning the full CheckEnrollmentAsync pipeline through a mock HTTP
    // handler. The service constructor takes only IConfiguration / ILoggerFactory,
    // so injecting a handler would require new test seams that the rest of the
    // suite does not yet need.
    // ---------------------------------------------------------------------------

    private static ChildCheckRequest MakeRequest(
        string firstName,
        string lastName,
        DateOnly dob,
        Guid? checkId = null) =>
        new()
        {
            CheckId = checkId ?? Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            DateOfBirth = dob
        };

    private static CheckEnrollmentStudentDetail MakeResponseRow(
        string? stdReqInd,
        double? mtchCnfd,
        string? sebtEligSts,
        string? stdFstNm = null,
        string? stdLstNm = null,
        string? stdDob = null)
    {
        var row = new CheckEnrollmentStudentDetail
        {
            StdReqInd = stdReqInd,
            MtchCnfd = mtchCnfd,
            StdFstNm = stdFstNm,
            StdLstNm = stdLstNm,
            StdDob = stdDob
        };
        if (sebtEligSts is not null)
        {
            row.AdditionalData["sebtEligSts"] = sebtEligSts;
        }
        return row;
    }

    private static Dictionary<string, ChildCheckRequest> IndexMap(params ChildCheckRequest[] children)
    {
        var map = new Dictionary<string, ChildCheckRequest>(StringComparer.Ordinal);
        for (var i = 0; i < children.Length; i++)
        {
            map[(i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)] = children[i];
        }
        return map;
    }

    [Fact]
    public void CorrelateResults_ExactMatch_HighConfidence_Eligible_ReturnsMatch()
    {
        // Adela regression: input matches CBMS exactly, score 100, eligible.
        var child = MakeRequest("Adela", "Ramsden", new DateOnly(2008, 3, 17));
        var children = new List<ChildCheckRequest> { child };
        var response = new CheckEnrollmentResponse
        {
            StdntDtls = new List<CheckEnrollmentStudentDetail>
            {
                MakeResponseRow("1", 100, "Y", "Adela", "Ramsden", "2008-03-17")
            }
        };

        var results = ColoradoEnrollmentCheckService.CorrelateResults(children, response, IndexMap(child));

        var result = Assert.Single(results);
        Assert.Equal(EnrollmentStatus.Match, result.Status);
        Assert.Equal(100, result.MatchConfidence);
        Assert.Equal(child.CheckId, result.CheckId);
    }

    [Fact]
    public void CorrelateResults_TypoedLastName_AboveThreshold_Eligible_ReturnsMatch()
    {
        // Hettie repro: input has typo on last name; CBMS still scores 97 with
        // the correct canonical name. Pre-fix, this returned NonMatch + null.
        var child = MakeRequest("Hettie", "HAINSWORTHh", new DateOnly(2008, 3, 12));
        var children = new List<ChildCheckRequest> { child };
        var response = new CheckEnrollmentResponse
        {
            StdntDtls = new List<CheckEnrollmentStudentDetail>
            {
                MakeResponseRow("1", 97, "Y", "Hettie", "HAINSWORTH", "2008-03-12")
            }
        };

        var results = ColoradoEnrollmentCheckService.CorrelateResults(children, response, IndexMap(child));

        var result = Assert.Single(results);
        Assert.Equal(EnrollmentStatus.Match, result.Status);
        Assert.Equal(97, result.MatchConfidence);
    }

    [Fact]
    public void CorrelateResults_ScoreEqualToThreshold_ReturnsNonMatch_WithConfidence()
    {
        // D5: threshold operator is strict greater-than. 90.0 -> NonMatch.
        var child = MakeRequest("Test1", "Persona1", new DateOnly(2010, 1, 1));
        var children = new List<ChildCheckRequest> { child };
        var response = new CheckEnrollmentResponse
        {
            StdntDtls = new List<CheckEnrollmentStudentDetail>
            {
                MakeResponseRow("1", 90.0, "Y")
            }
        };

        var results = ColoradoEnrollmentCheckService.CorrelateResults(children, response, IndexMap(child));

        var result = Assert.Single(results);
        Assert.Equal(EnrollmentStatus.NonMatch, result.Status);
        Assert.Equal(90.0, result.MatchConfidence);
    }

    [Fact]
    public void CorrelateResults_ScoreJustAboveThreshold_Eligible_ReturnsMatch()
    {
        // D5: 90.5 is strictly greater than 90 -> Match (eligibility-gated).
        var child = MakeRequest("Test2", "Persona2", new DateOnly(2010, 1, 1));
        var children = new List<ChildCheckRequest> { child };
        var response = new CheckEnrollmentResponse
        {
            StdntDtls = new List<CheckEnrollmentStudentDetail>
            {
                MakeResponseRow("1", 90.5, "Y")
            }
        };

        var results = ColoradoEnrollmentCheckService.CorrelateResults(children, response, IndexMap(child));

        var result = Assert.Single(results);
        Assert.Equal(EnrollmentStatus.Match, result.Status);
        Assert.Equal(90.5, result.MatchConfidence);
    }

    [Fact]
    public void CorrelateResults_SubThresholdScore_Eligible_ReturnsNonMatch_ButPreservesScore()
    {
        // Score 85 (sub-threshold). Status NonMatch but MatchConfidence is still
        // surfaced so logs/UI can explain how close we were.
        var child = MakeRequest("Test3", "Persona3", new DateOnly(2010, 1, 1));
        var children = new List<ChildCheckRequest> { child };
        var response = new CheckEnrollmentResponse
        {
            StdntDtls = new List<CheckEnrollmentStudentDetail>
            {
                MakeResponseRow("1", 85, "Y")
            }
        };

        var results = ColoradoEnrollmentCheckService.CorrelateResults(children, response, IndexMap(child));

        var result = Assert.Single(results);
        Assert.Equal(EnrollmentStatus.NonMatch, result.Status);
        Assert.Equal(85, result.MatchConfidence);
    }

    [Fact]
    public void CorrelateResults_NullMtchCnfd_ReturnsNonMatch_WithNullConfidence()
    {
        var child = MakeRequest("Test4", "Persona4", new DateOnly(2010, 1, 1));
        var children = new List<ChildCheckRequest> { child };
        var response = new CheckEnrollmentResponse
        {
            StdntDtls = new List<CheckEnrollmentStudentDetail>
            {
                MakeResponseRow("1", null, "Y")
            }
        };

        var results = ColoradoEnrollmentCheckService.CorrelateResults(children, response, IndexMap(child));

        var result = Assert.Single(results);
        Assert.Equal(EnrollmentStatus.NonMatch, result.Status);
        Assert.Null(result.MatchConfidence);
    }

    [Fact]
    public void CorrelateResults_NoRowsForChild_ReturnsNonMatch_WithNoMatchingRecordMessage()
    {
        var child = MakeRequest("Test5", "Persona5", new DateOnly(2010, 1, 1));
        var children = new List<ChildCheckRequest> { child };
        var response = new CheckEnrollmentResponse
        {
            StdntDtls = new List<CheckEnrollmentStudentDetail>()
        };

        var results = ColoradoEnrollmentCheckService.CorrelateResults(children, response, IndexMap(child));

        var result = Assert.Single(results);
        Assert.Equal(EnrollmentStatus.NonMatch, result.Status);
        Assert.Null(result.MatchConfidence);
        Assert.Contains("No matching record", result.StatusMessage);
    }

    [Fact]
    public void CorrelateResults_TwoRowsSameStdReqInd_BothEligible_HighestScoreWins()
    {
        var child = MakeRequest("Test6", "Persona6", new DateOnly(2010, 1, 1));
        var children = new List<ChildCheckRequest> { child };
        var response = new CheckEnrollmentResponse
        {
            StdntDtls = new List<CheckEnrollmentStudentDetail>
            {
                MakeResponseRow("1", 92, "Y"),
                MakeResponseRow("1", 97, "Y")
            }
        };

        var results = ColoradoEnrollmentCheckService.CorrelateResults(children, response, IndexMap(child));

        var result = Assert.Single(results);
        Assert.Equal(EnrollmentStatus.Match, result.Status);
        Assert.Equal(97, result.MatchConfidence);
    }

    [Fact]
    public void CorrelateResults_TwoRowsSameStdReqInd_HighestScoreIneligible_StatusFollowsBestRow()
    {
        // D3: highest mtchCnfd wins regardless of eligibility flag.
        // D12: status flows through MapEnrollmentStatus on the winning row, so a
        // 97-confidence row with sebtEligSts="N" maps to NonMatch.
        var child = MakeRequest("Test7", "Persona7", new DateOnly(2010, 1, 1));
        var children = new List<ChildCheckRequest> { child };
        var response = new CheckEnrollmentResponse
        {
            StdntDtls = new List<CheckEnrollmentStudentDetail>
            {
                MakeResponseRow("1", 92, "Y"),
                MakeResponseRow("1", 97, "N")
            }
        };

        var results = ColoradoEnrollmentCheckService.CorrelateResults(children, response, IndexMap(child));

        var result = Assert.Single(results);
        Assert.Equal(EnrollmentStatus.NonMatch, result.Status);
        Assert.Equal(97, result.MatchConfidence);
    }

    [Fact]
    public void CorrelateResults_TwoRowsBothNullScore_DeterministicNonMatch()
    {
        var child = MakeRequest("Test8", "Persona8", new DateOnly(2010, 1, 1));
        var children = new List<ChildCheckRequest> { child };
        var response = new CheckEnrollmentResponse
        {
            StdntDtls = new List<CheckEnrollmentStudentDetail>
            {
                MakeResponseRow("1", null, "Y"),
                MakeResponseRow("1", null, "N")
            }
        };

        var results = ColoradoEnrollmentCheckService.CorrelateResults(children, response, IndexMap(child));

        var result = Assert.Single(results);
        Assert.Equal(EnrollmentStatus.NonMatch, result.Status);
        Assert.Null(result.MatchConfidence);
    }

    [Fact]
    public void CorrelateResults_OrphanRowsWithoutStdReqInd_AreDropped_ChildResolvesAsNonMatch()
    {
        // D7: orphan rows (null/empty StdReqInd) are dropped, not name+DOB
        // fallback-matched. The child has no surviving row, so NonMatch.
        var child = MakeRequest("Test9", "Persona9", new DateOnly(2010, 1, 1));
        var children = new List<ChildCheckRequest> { child };
        var response = new CheckEnrollmentResponse
        {
            StdntDtls = new List<CheckEnrollmentStudentDetail>
            {
                MakeResponseRow(null, 99, "Y", "Test9", "Persona9", "2010-01-01"),
                MakeResponseRow("", 95, "Y", "Test9", "Persona9", "2010-01-01")
            }
        };

        var results = ColoradoEnrollmentCheckService.CorrelateResults(children, response, IndexMap(child));

        var result = Assert.Single(results);
        Assert.Equal(EnrollmentStatus.NonMatch, result.Status);
        Assert.Null(result.MatchConfidence);
    }

    [Fact]
    public void CorrelateResults_MultipleChildren_RowsInterleavedOutOfOrder_EachCorrelatesByStdReqInd()
    {
        var c1 = MakeRequest("Adela", "Ramsden", new DateOnly(2008, 3, 17));
        var c2 = MakeRequest("Hettie", "HAINSWORTHh", new DateOnly(2008, 3, 12));
        var c3 = MakeRequest("Test10", "Persona10", new DateOnly(2010, 1, 1));
        var children = new List<ChildCheckRequest> { c1, c2, c3 };

        var response = new CheckEnrollmentResponse
        {
            StdntDtls = new List<CheckEnrollmentStudentDetail>
            {
                // Out-of-order: c3, c1, c2
                MakeResponseRow("3", 80, "N"),
                MakeResponseRow("1", 100, "Y"),
                MakeResponseRow("2", 97, "Y")
            }
        };

        var results = ColoradoEnrollmentCheckService.CorrelateResults(children, response, IndexMap(c1, c2, c3));

        Assert.Equal(3, results.Count);

        Assert.Equal(c1.CheckId, results[0].CheckId);
        Assert.Equal(EnrollmentStatus.Match, results[0].Status);
        Assert.Equal(100, results[0].MatchConfidence);

        Assert.Equal(c2.CheckId, results[1].CheckId);
        Assert.Equal(EnrollmentStatus.Match, results[1].Status);
        Assert.Equal(97, results[1].MatchConfidence);

        Assert.Equal(c3.CheckId, results[2].CheckId);
        Assert.Equal(EnrollmentStatus.NonMatch, results[2].Status);
        Assert.Equal(80, results[2].MatchConfidence);
    }

    // ---------------------------------------------------------------------------
    // Outbound request shape: per-row 1-based StdReqInd, names/DOB/school code
    // pass through unchanged. Tested via the BuildCbmsRequests helper to keep the
    // surface narrow. (See note above; closes the test gap called out in D9.)
    // ---------------------------------------------------------------------------

    [Fact]
    public void BuildCbmsRequests_ThreeChildren_AssignsOneBasedStdReqInd()
    {
        var c1 = MakeRequest("Adela", "Ramsden", new DateOnly(2008, 3, 17));
        var c2 = MakeRequest("Hettie", "HAINSWORTHh", new DateOnly(2008, 3, 12));
        var c3 = MakeRequest("Test11", "Persona11", new DateOnly(2010, 6, 1));
        c3 = new ChildCheckRequest
        {
            CheckId = c3.CheckId,
            FirstName = c3.FirstName,
            LastName = c3.LastName,
            DateOfBirth = c3.DateOfBirth,
            SchoolCode = "12345"
        };
        var children = new List<ChildCheckRequest> { c1, c2, c3 };

        var requests = ColoradoEnrollmentCheckService.BuildCbmsRequests(children);

        Assert.Equal(3, requests.Count);

        Assert.Equal("1", requests[0].StdReqInd);
        Assert.Equal("Adela", requests[0].StdFirstName);
        Assert.Equal("Ramsden", requests[0].StdLastName);
        Assert.Equal("2008-03-17", requests[0].StdDob);

        Assert.Equal("2", requests[1].StdReqInd);
        Assert.Equal("Hettie", requests[1].StdFirstName);
        Assert.Equal("HAINSWORTHh", requests[1].StdLastName);
        Assert.Equal("2008-03-12", requests[1].StdDob);

        Assert.Equal("3", requests[2].StdReqInd);
        Assert.Equal("Test11", requests[2].StdFirstName);
        Assert.Equal("Persona11", requests[2].StdLastName);
        Assert.Equal("2010-06-01", requests[2].StdDob);
        Assert.Equal("12345", requests[2].StdSchlCd);
    }
}
