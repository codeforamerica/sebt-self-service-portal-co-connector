using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace SEBT.Portal.StatePlugins.CO.CbmsApi;

/// <summary>
/// Delegating handler that logs the raw HTTP request/response timing,
/// separate from Kiota's serialization overhead. Captures method, URL
/// path, status code, and elapsed milliseconds at the transport layer.
/// </summary>
internal sealed class CbmsHttpTimingHandler : DelegatingHandler
{
    private readonly ILogger _logger;

    public CbmsHttpTimingHandler(HttpMessageHandler innerHandler, ILogger logger)
        : base(innerHandler)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var method = request.Method;
        var path = request.RequestUri?.PathAndQuery ?? "(unknown)";

        _logger.LogInformation("CBMS HTTP {Method} {Path}: sending request", method, path);

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            sw.Stop();

            _logger.LogInformation(
                "CBMS HTTP {Method} {Path}: {StatusCode} in {ElapsedMs}ms",
                method, path, (int)response.StatusCode, sw.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();

            _logger.LogWarning(
                "CBMS HTTP {Method} {Path}: failed after {ElapsedMs}ms — {ErrorMessage}",
                method, path, sw.ElapsedMilliseconds, ex.Message);

            throw;
        }
    }
}
