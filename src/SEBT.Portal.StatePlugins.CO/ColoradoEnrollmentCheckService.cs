using System.Composition;
using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;
using SEBT.Portal.StatePlugins.CO.Cbms;
using SEBT.Portal.StatePlugins.CO.CbmsApi;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;
using CbmsCheckEnrollmentRequest = SEBT.Portal.StatePlugins.CO.CbmsApi.Models.CheckEnrollmentRequest;

namespace SEBT.Portal.StatePlugins.CO;

/// <summary>
/// Colorado implementation of the enrollment check service.
/// Calls the CBMS API's check-enrollment endpoint to verify child eligibility
/// for Summer EBT benefits.
/// </summary>
[Export(typeof(IStatePlugin))]
[ExportMetadata("StateCode", "CO")]
public class ColoradoEnrollmentCheckService : ColoradoCbmsServiceBase, IEnrollmentCheckService
{
    private const string BaseUrlConfigKey = "COConnector:CbmsApiBaseUrl";
    private const string ApiKeyConfigKey = "COConnector:CbmsApiKey";

    private readonly IConfiguration? _configuration;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes the service. The host should supply <paramref name="configuration"/> so the
    /// CBMS API base URL and API key are read from configuration (or set via env vars).
    /// </summary>
    /// <param name="configuration">Optional. The application configuration; when null, only env vars are checked.</param>
    /// <param name="loggerFactory">Optional. Logger factory for diagnostics.</param>
    [ImportingConstructor]
    public ColoradoEnrollmentCheckService(
        [Import(AllowDefault = true)] IConfiguration? configuration = null,
        [Import(AllowDefault = true)] ILoggerFactory? loggerFactory = null)
    : base(null, loggerFactory.CreateLogger<ColoradoEnrollmentCheckService>())
    {
        _configuration = configuration;
        _logger = loggerFactory?.CreateLogger<ColoradoEnrollmentCheckService>() ?? NullLogger<ColoradoEnrollmentCheckService>.Instance;
    }

    /// <inheritdoc />
    public async Task<EnrollmentCheckResult> CheckEnrollmentAsync(
        EnrollmentCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        if (request.Children.Count == 0)
        {
            return new EnrollmentCheckResult
            {
                Results = new List<ChildCheckResult>()
            };
        }

        var options = CbmsOptionsHelper.GetCbmsOptions(_configuration);
        if (!options.IsConfigured)
        {
            return null;
        }
        
        var client = GetOrCreateClient(options);

        // Map each child to a CBMS CheckEnrollmentRequest
        var cbmsRequests = request.Children.Select(child => new CbmsCheckEnrollmentRequest
        {
            StdFirstName = child.FirstName,
            StdLastName = child.LastName,
            StdDob = child.DateOfBirth.ToString("yyyy-MM-dd"),
            StdSchlCd = child.SchoolCode
        }).ToList();

        _logger.LogInformation(
            "CBMS EnrollmentCheck: starting request for {ChildCount} child(ren) (POST /sebt/check-enrollment)",
            cbmsRequests.Count);
        var sw = Stopwatch.StartNew();
        var cbmsResponse = await client.Sebt.CheckEnrollment.PostAsync(cbmsRequests, cancellationToken: cancellationToken);
        sw.Stop();
        _logger.LogInformation(
            "CBMS EnrollmentCheck: completed in {ElapsedMs}ms, returned {DetailCount} student detail(s)",
            sw.ElapsedMilliseconds, cbmsResponse?.StdntDtls?.Count ?? 0);

        var results = CorrelateResults(request.Children, cbmsResponse);

        return new EnrollmentCheckResult
        {
            Results = results,
            ResponseMessage = cbmsResponse?.RespMsg
        };
    }

    /// <summary>
    /// Correlates CBMS API response student details back to the original request children.
    /// CBMS doesn't echo correlation IDs, so we match on first name + last name + date of birth.
    /// </summary>
    private static IList<ChildCheckResult> CorrelateResults(
        IList<ChildCheckRequest> requestChildren,
        CheckEnrollmentResponse? cbmsResponse)
    {
        var results = new List<ChildCheckResult>();
        var studentDetails = cbmsResponse?.StdntDtls ?? new List<CheckEnrollmentStudentDetail>();

        foreach (var child in requestChildren)
        {
            var matchingDetail = studentDetails.FirstOrDefault(detail =>
                string.Equals(detail.StdFstNm, child.FirstName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(detail.StdLstNm, child.LastName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(detail.StdDob, child.DateOfBirth.ToString("yyyy-MM-dd"), StringComparison.Ordinal));

            if (matchingDetail != null)
            {
                var sebtEligSts = matchingDetail.AdditionalData.TryGetValue("sebtEligSts", out var value)
                    ? value?.ToString()
                    : null;

                results.Add(new ChildCheckResult
                {
                    CheckId = child.CheckId,
                    FirstName = child.FirstName,
                    LastName = child.LastName,
                    DateOfBirth = child.DateOfBirth,
                    Status = MapEnrollmentStatus(sebtEligSts),
                    MatchConfidence = matchingDetail.MtchCnfd,
                    StatusMessage = sebtEligSts
                });
            }
            else
            {
                // No matching student detail found in the response
                results.Add(new ChildCheckResult
                {
                    CheckId = child.CheckId,
                    FirstName = child.FirstName,
                    LastName = child.LastName,
                    DateOfBirth = child.DateOfBirth,
                    Status = EnrollmentStatus.NonMatch,
                    StatusMessage = "No matching record found in CBMS response"
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Maps CBMS sebtEligSts values (Y/N) to the standard <see cref="EnrollmentStatus"/> enum.
    /// </summary>
    internal static EnrollmentStatus MapEnrollmentStatus(string? sebtEligSts)
    {
        return sebtEligSts?.ToUpperInvariant() switch
        {
            "Y" => EnrollmentStatus.Match,
            _ => EnrollmentStatus.NonMatch
        };
    }
}
